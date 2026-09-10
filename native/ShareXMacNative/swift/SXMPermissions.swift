// Permission and capability probing.
//
// Permission state is never cached permanently: IPlatformCapabilities requires a
// fresh read after relevant OS events (docs/INTERFACES-AND-DATA.md).
// This code only *reads* and *requests* normal user permissions. It never
// modifies TCC, SIP or Gatekeeper state.

import Foundation
import AppKit
import AVFoundation
import ApplicationServices
import CoreGraphics

enum SXMPermissionState: String {
    case granted
    case denied
    case required          // not yet asked, or ask-again is possible
    case unsupported
    case notDetermined
}

enum SXMPermissions {

    /// Screen & System Audio Recording (TCC kTCCServiceScreenCapture).
    /// CGPreflightScreenCaptureAccess does not raise a prompt.
    static func screenRecording() -> SXMPermissionState {
        CGPreflightScreenCaptureAccess() ? .granted : .required
    }

    /// Raises the system prompt exactly once per process/TCC decision. Returns
    /// false if the user has already denied; macOS then requires a manual change
    /// in System Settings, which this app must not perform for the user.
    static func requestScreenRecording() -> Bool {
        CGRequestScreenCaptureAccess()
    }

    /// Ensures the app has actually ASKED for Screen Recording before reporting
    /// that the permission is missing.
    ///
    /// This exists because of a real bug: the capture path used to gate purely on
    /// `CGPreflightScreenCaptureAccess()`, which never prompts. The app therefore
    /// failed with "permission required" without ever giving macOS the chance to
    /// show the dialog, and until an app requests once it may not even be listed
    /// in System Settings > Privacy & Security > Screen & System Audio Recording.
    /// The user was left with nothing to grant.
    ///
    /// `CGRequestScreenCaptureAccess()` prompts on the first call and, once a
    /// decision is stored, returns it without prompting again. So calling it here
    /// is safe and idempotent: it converts "never asked" into a real prompt and
    /// leaves an existing decision untouched.
    ///
    /// Returns the resulting state. A stored denial stays denied; this code never
    /// modifies TCC and never retries in a loop.
    static func ensureScreenRecording() -> SXMPermissionState {
        if CGPreflightScreenCaptureAccess() {
            return .granted
        }

        // Prompts the first time; returns the stored decision afterwards.
        if CGRequestScreenCaptureAccess() {
            return .granted
        }

        // Re-preflight: the user may have granted in the dialog while the request
        // call had already returned, and a fresh read is cheap.
        return CGPreflightScreenCaptureAccess() ? .granted : .required
    }

    static func accessibility() -> SXMPermissionState {
        AXIsProcessTrusted() ? .granted : .required
    }

    /// Shows the standard Accessibility prompt when `prompt` is true.
    static func requestAccessibility(prompt: Bool) -> SXMPermissionState {
        // Literal key avoids the Unmanaged/CFString import difference between SDKs.
        let options = ["AXTrustedCheckOptionPrompt": prompt] as CFDictionary
        return AXIsProcessTrustedWithOptions(options) ? .granted : .required
    }

    static func microphone() -> SXMPermissionState {
        switch AVCaptureDevice.authorizationStatus(for: .audio) {
        case .authorized: return .granted
        case .denied: return .denied
        case .restricted: return .denied
        case .notDetermined: return .notDetermined
        @unknown default: return .required
        }
    }

    static func requestMicrophone(_ completion: @escaping (SXMPermissionState) -> Void) {
        AVCaptureDevice.requestAccess(for: .audio) { granted in
            completion(granted ? .granted : .denied)
        }
    }

    static func snapshot() -> [String: Any] {
        [
            "screenRecording": screenRecording().rawValue,
            "accessibility": accessibility().rawValue,
            "microphone": microphone().rawValue
        ]
    }
}

enum SXMCapabilities {

    static func snapshot() -> [String: Any] {
        let info = ProcessInfo.processInfo
        let version = info.operatingSystemVersion
        let bundle = Bundle.main

        return [
            "abiVersion": Int(SXM_ABI),
            "osVersion": "\(version.majorVersion).\(version.minorVersion).\(version.patchVersion)",
            "osVersionString": info.operatingSystemVersionString,
            "architecture": SXMCapabilities.architecture,
            "bundleIdentifier": bundle.bundleIdentifier ?? "",
            "bundlePath": bundle.bundlePath,
            "isBundled": bundle.bundleIdentifier != nil,
            "processName": info.processName,
            "features": features(),
            "permissions": SXMPermissions.snapshot()
        ]
    }

    static var architecture: String {
        #if arch(arm64)
        return "arm64"
        #elseif arch(x86_64)
        return "x86_64"
        #else
        return "unknown"
        #endif
    }

    /// Reports what this build can actually do on the running OS. A capability
    /// that is absent stays absent; it is never silently substituted.
    static func features() -> [String: Any] {
        var features: [String: Any] = [:]
        features["screenCaptureKit"] = true
        features["screenshotManager"] = true              // SCScreenshotManager, macOS 14+
        features["windowCapture"] = true
        features["systemAudioCapture"] = true             // SCStreamConfiguration.capturesAudio
        if #available(macOS 15.0, *) {
            features["microphoneCapture"] = true          // SCStreamConfiguration.captureMicrophone
        } else {
            features["microphoneCapture"] = false
        }
        features["visionOcr"] = true
        features["keychain"] = true
        features["globalHotkeys"] = true
        // Explicitly false: macOS exposes no public API for these upstream behaviours.
        // See docs/MACOS-LIMITATIONS.md; they are platform exceptions, not TODOs.
        features["foreignWindowAlwaysOnTop"] = false
        features["foreignWindowResize"] = false
        return features
    }
}
