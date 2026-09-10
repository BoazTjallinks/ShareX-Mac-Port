// Credential storage via the Security framework.
//
// Replaces upstream's Windows DPAPI-encrypted settings (PROJECT-SPEC.md
// section 10: "New local credentials go to Keychain"). Generic-password items
// are scoped to a single service so this app's credentials never collide
// with, or become visible alongside, another app's Keychain items.
//
// Secrets never appear in a log, an error message, or a diagnostic payload.
// keychain.list returns account names only; keychain.get returns a secret in
// its own minimal payload with nothing else mixed in.

import Foundation
import Security

enum SXMKeychain {

    private static let service = "com.tjallinks.sharexmac"

    static func set(_ ctx: SXMContext) throws {
        let account = try ctx.string("account")
        let secret = try ctx.string("secret")

        var secretBytes = Array(secret.utf8)
        defer {
            // Zero the one buffer we hold directly, for as long as Swift gives
            // us that control. String's own backing storage, and any copy the
            // Security framework/XPC layer made while storing the item, are
            // outside what this process can scrub — that limitation is real
            // and is not hidden here.
            for index in secretBytes.indices { secretBytes[index] = 0 }
        }
        let data = Data(secretBytes)

        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account
        ]

        // Update first so an existing item is replaced rather than rejected as
        // a duplicate; only add when there is nothing to update yet.
        let updateAttributes: [String: Any] = [
            kSecValueData as String: data,
            kSecAttrAccessible as String: kSecAttrAccessibleWhenUnlocked
        ]
        let updateStatus = SecItemUpdate(query as CFDictionary, updateAttributes as CFDictionary)

        if updateStatus == errSecItemNotFound {
            var addQuery = query
            addQuery[kSecValueData as String] = data
            addQuery[kSecAttrAccessible as String] = kSecAttrAccessibleWhenUnlocked
            let addStatus = SecItemAdd(addQuery as CFDictionary, nil)
            guard addStatus == errSecSuccess else {
                throw SXMFailure(.internalFailure, "Keychain add failed with status \(addStatus).",
                                 detail: ["account": account])
            }
        } else if updateStatus != errSecSuccess {
            throw SXMFailure(.internalFailure, "Keychain update failed with status \(updateStatus).",
                             detail: ["account": account])
        }

        ctx.succeed(["account": account, "stored": true])
    }

    static func get(_ ctx: SXMContext) throws {
        let account = try ctx.string("account")
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
            kSecReturnData as String: true,
            kSecMatchLimit as String: kSecMatchLimitOne
        ]

        var result: CFTypeRef?
        let status = SecItemCopyMatching(query as CFDictionary, &result)

        guard status != errSecItemNotFound else {
            throw SXMFailure(.credentialRequired, "No Keychain item for account '\(account)'.",
                             detail: ["account": account])
        }
        guard status == errSecSuccess, let data = result as? Data,
              let secret = String(data: data, encoding: .utf8) else {
            // This branch never had a real secret to leak; it only ever
            // reaches here on failure, so the OSStatus alone is safe to report.
            throw SXMFailure(.internalFailure, "Keychain read failed with status \(status).",
                             detail: ["account": account])
        }

        // A dedicated, minimal payload: nothing else travels alongside a secret.
        ctx.succeed(["secret": secret])
    }

    static func delete(_ ctx: SXMContext) throws {
        let account = try ctx.string("account")
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account
        ]

        let status = SecItemDelete(query as CFDictionary)
        guard status == errSecSuccess || status == errSecItemNotFound else {
            throw SXMFailure(.internalFailure, "Keychain delete failed with status \(status).",
                             detail: ["account": account])
        }
        ctx.succeed(["account": account, "deleted": true])
    }

    static func list(_ ctx: SXMContext) throws {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecReturnAttributes as String: true,
            kSecMatchLimit as String: kSecMatchLimitAll
        ]

        var result: CFTypeRef?
        let status = SecItemCopyMatching(query as CFDictionary, &result)

        if status == errSecItemNotFound {
            ctx.succeed(["accounts": [String]()])
            return
        }
        guard status == errSecSuccess, let items = result as? [[String: Any]] else {
            throw SXMFailure(.internalFailure, "Keychain list failed with status \(status).")
        }

        // kSecReturnAttributes never yields kSecValueData, so there is no
        // secret in these entries to accidentally include.
        let accounts = items.compactMap { $0[kSecAttrAccount as String] as? String }
        ctx.succeed(["accounts": accounts])
    }
}
