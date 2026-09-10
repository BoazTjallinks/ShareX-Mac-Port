// Registry of live recording sessions.
//
// "ONE owner for the stream and encoder" (PROJECT-SPEC.md section 6, design
// requirement 1) is enforced per-session inside SXMRecordingSession; this
// registry just lets independent recording.* route calls find the right
// session by id. sessionId doubles as the SXMDispatcher event subscription id
// for that session's recording.state/recording.progress events, so it is a
// UInt64 (matching sxm_event_fn's subscription_id), not a UUID string.

import Foundation

final class SXMRecordingRegistry {
    static let shared = SXMRecordingRegistry()

    private let lock = NSLock()
    private var sessions: [UInt64: SXMRecordingSession] = [:]
    private var nextId: UInt64 = 1

    private init() {}

    func allocateSessionId() -> UInt64 {
        lock.lock(); defer { lock.unlock() }
        let id = nextId
        nextId += 1
        return id
    }

    func insert(_ session: SXMRecordingSession) {
        lock.lock(); defer { lock.unlock() }
        sessions[session.sessionId] = session
    }

    func find(_ sessionId: UInt64) -> SXMRecordingSession? {
        lock.lock(); defer { lock.unlock() }
        return sessions[sessionId]
    }

    func remove(_ sessionId: UInt64) {
        lock.lock(); defer { lock.unlock() }
        sessions.removeValue(forKey: sessionId)
    }
}
