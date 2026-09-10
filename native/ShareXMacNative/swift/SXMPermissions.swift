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
