// ShareX-Mac native bridge — dispatcher core.
//
// Owns the operation registry, request routing, cancellation and the bounded
// binary result store. Every native error becomes a structured result code plus
// a JSON payload; no Swift error crosses the ABI (PROJECT-SPEC.md section 4).

import Foundation

// Mirrors sxm_result_code in include/sxm_abi.h. Keep the two in sync.
@objc public enum SXMCode: Int32 {
    case ok = 0
    case userCancelled = 1
    case permissionRequired = 2
    case permissionDenied = 3
    case unsupportedCapability = 4
    case targetDisappeared = 5
    case invalidConfiguration = 6
    case invalidInput = 7
    case credentialRequired = 8
    case remoteRejected = 9
    case remoteOutcomeUnknown = 10
    case localIOFailure = 11
    case encoderFailure = 12
    case internalFailure = 13
    case unknownOperation = 14
    case abiMismatch = 15
}

/// Thrown inside handlers; converted to a result code at the dispatcher edge.
struct SXMFailure: Error {
    let code: SXMCode
    let message: String
    let detail: [String: Any]?

    init(_ code: SXMCode, _ message: String, detail: [String: Any]? = nil) {
        self.code = code
        self.message = message
        self.detail = detail
    }
}

/// Per-operation state handed to a handler.
final class SXMContext {
    let operationId: UInt64
    let args: [String: Any]
    private let registry: SXMDispatcher
    private let lock = NSLock()
    private var finished = false
    private var cancelledFlag = false
    private var cancelHook: (() -> Void)?

    init(operationId: UInt64, args: [String: Any], registry: SXMDispatcher) {
        self.operationId = operationId
        self.args = args
        self.registry = registry
    }

    var isCancelled: Bool {
        lock.lock(); defer { lock.unlock() }
        return cancelledFlag
    }

    /// Registers an idempotent cancellation hook. Runs immediately if the
    /// operation was already cancelled before the hook was installed.
    func onCancel(_ hook: @escaping () -> Void) {
        lock.lock()
        if cancelledFlag {
            lock.unlock()
            hook()
            return
        }
        cancelHook = hook
        lock.unlock()
    }

    /// Called by the dispatcher. Idempotent, per the ABI cancellation contract.
    func markCancelled() {
        lock.lock()
        if cancelledFlag || finished { lock.unlock(); return }
        cancelledFlag = true
        let hook = cancelHook
        cancelHook = nil
        lock.unlock()
        hook?()
    }

    func succeed(_ payload: [String: Any] = [:], blob: Data? = nil) {
        finish(.ok, payload, blob)
    }

    func fail(_ failure: SXMFailure) {
        var payload: [String: Any] = ["error": failure.message]
        if let detail = failure.detail {
            for (k, v) in detail { payload[k] = v }
        }
        finish(failure.code, payload, nil)
    }

    func fail(_ code: SXMCode, _ message: String) {
        finish(code, ["error": message], nil)
    }

    func cancelled() {
        finish(.userCancelled, ["error": "Operation cancelled."], nil)
    }

    private func finish(_ code: SXMCode, _ payload: [String: Any], _ blob: Data?) {
        lock.lock()
        if finished { lock.unlock(); return }
        finished = true
        cancelHook = nil
        lock.unlock()
        registry.complete(operationId: operationId, code: code, payload: payload, blob: blob)
    }
}

typealias SXMHandler = (SXMContext) throws -> Void

@objc(SXMDispatcher)
public final class SXMDispatcher: NSObject {

    private static let instance = SXMDispatcher()
    @objc public class var shared: SXMDispatcher { instance }

    private let lock = NSLock()
    private var contexts: [UInt64: SXMContext] = [:]
    private var completions: [UInt64: (Int32, Data?) -> Void] = [:]
    private var blobs: [UInt64: Data] = [:]
    private var eventSink: ((UInt64, Data?) -> Void)?

    /// Bounded work queue: capture/encode work must never run on the UI thread.
    let workQueue = DispatchQueue(label: "com.tjallinks.sharexmac.native.work",
                                  qos: .userInitiated,
                                  attributes: .concurrent)

    private let routes: [String: SXMHandler]

    private override init() {
        routes = SXMRoutes.build()
        super.init()
    }

    // MARK: - Exported surface

    @objc(setEventSink:) public func setEventSink(_ sink: ((UInt64, NSData?) -> Void)?) {
        lock.lock(); defer { lock.unlock() }
        if let sink {
            eventSink = { id, data in sink(id, data as NSData?) }
        } else {
            eventSink = nil
        }
    }

    @objc(beginWithRequest:operationId:completion:) public func begin(request: NSData,
                            operationId: UInt64,
                            completion: @escaping (Int32, NSData?) -> Void) -> Int32 {
        let object: Any
        do {
            object = try JSONSerialization.jsonObject(with: request as Data, options: [])
        } catch {
            return SXMCode.invalidInput.rawValue
        }
        guard let envelope = object as? [String: Any],
              let op = envelope["op"] as? String, !op.isEmpty else {
            return SXMCode.invalidInput.rawValue
        }
        if let requested = envelope["abi"] as? Int, requested != Int(SXM_ABI) {
            return SXMCode.abiMismatch.rawValue
        }
        guard let handler = routes[op] else {
            return SXMCode.unknownOperation.rawValue
        }

        let args = (envelope["args"] as? [String: Any]) ?? [:]
        let context = SXMContext(operationId: operationId, args: args, registry: self)

        lock.lock()
        contexts[operationId] = context
        completions[operationId] = { code, data in completion(code, data as NSData?) }
        lock.unlock()

        workQueue.async { [weak self] in
            guard self != nil else { return }
            do {
                try handler(context)
            } catch let failure as SXMFailure {
                context.fail(failure)
            } catch {
                context.fail(.internalFailure, "Unhandled native error: \(error)")
            }
        }
        return SXMCode.ok.rawValue
    }

    @objc(cancelWithOperationId:) public func cancel(operationId: UInt64) -> Int32 {
        lock.lock()
        let context = contexts[operationId]
        lock.unlock()
        // Cancellation of an unknown or already-terminal operation is a no-op,
        // not an error: the ABI requires idempotence.
        context?.markCancelled()
        return SXMCode.ok.rawValue
    }

    // Not named "release": Objective-C ARC reserves the release method family.
    @objc(disposeOperationWithId:) public func disposeOperation(operationId: UInt64) -> Int32 {
        lock.lock()
        contexts.removeValue(forKey: operationId)
        completions.removeValue(forKey: operationId)
        blobs.removeValue(forKey: operationId)
        lock.unlock()
        return SXMCode.ok.rawValue
    }

    @objc(blobForOperationId:) public func blob(forOperationId operationId: UInt64) -> NSData? {
        lock.lock(); defer { lock.unlock() }
        guard let data = blobs[operationId] else { return nil }
        return data as NSData
    }

    // MARK: - Internal

    func complete(operationId: UInt64, code: SXMCode, payload: [String: Any], blob: Data?) {
        lock.lock()
        let completion = completions.removeValue(forKey: operationId)
        if let blob {
            blobs[operationId] = blob
        }
        lock.unlock()

        guard let completion else { return }

        var body = payload
        body["ok"] = (code == .ok)
        body["code"] = code.rawValue
        if let blob { body["blobLength"] = blob.count }

        let data = SXMDispatcher.encode(body)
        completion(code.rawValue, data)
    }

    /// Publishes a low-rate event (recording state, hotkey press, display change).
    /// Media data never travels this path.
    func emit(subscription: UInt64, event: [String: Any]) {
        lock.lock()
        let sink = eventSink
        lock.unlock()
        guard let sink else { return }
        sink(subscription, SXMDispatcher.encode(event))
    }

    static func encode(_ body: [String: Any]) -> Data {
        if let data = try? JSONSerialization.data(withJSONObject: body, options: []) {
            return data
        }
        // Never let a serialization failure become a silent success.
        let fallback = #"{"ok":false,"code":13,"error":"Result serialization failed."}"#
        return Data(fallback.utf8)
    }
}

let SXM_ABI: UInt32 = 1
