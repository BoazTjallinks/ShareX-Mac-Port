/*
 * ShareX-Mac native bridge — public C ABI.
 *
 * Contract (PROJECT-SPEC.md section 4, docs/INTERFACES-AND-DATA.md "Native ABI sketch"):
 *   - Fixed-width integers, UTF-8 byte buffers with explicit lengths, opaque
 *     operation ids, explicit error codes, explicit release.
 *   - sxm_begin() copies request bytes before returning. It either fails
 *     immediately (negative/positive error, no callback) or returns SXM_OK with
 *     a valid operation id that completes exactly once, asynchronously.
 *   - Completion payload is BORROWED for the duration of the callback.
 *   - sxm_cancel() is idempotent and is NOT the terminal acknowledgement.
 *   - sxm_release() is legal only after the terminal event; it is idempotent.
 *   - No exception, Swift object or Objective-C object crosses this boundary.
 *
 * This header is the single source of truth for the managed P/Invoke layer in
 * src/ShareX.Platform.Mac.
 */
#ifndef SHAREX_MAC_ABI_H
#define SHAREX_MAC_ABI_H

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/* Bumped only for incompatible ABI changes. */
#define SXM_ABI_VERSION 1u

/* Result codes. Mirrors the error taxonomy in docs/INTERFACES-AND-DATA.md. */
typedef enum {
    SXM_OK                     = 0,
    SXM_USER_CANCELLED         = 1,
    SXM_PERMISSION_REQUIRED    = 2,
    SXM_PERMISSION_DENIED      = 3,
    SXM_UNSUPPORTED_CAPABILITY = 4,
    SXM_TARGET_DISAPPEARED     = 5,
    SXM_INVALID_CONFIGURATION  = 6,
    SXM_INVALID_INPUT          = 7,
    SXM_CREDENTIAL_REQUIRED    = 8,
    SXM_REMOTE_REJECTED        = 9,
    SXM_REMOTE_OUTCOME_UNKNOWN = 10,
    SXM_LOCAL_IO_FAILURE       = 11,
    SXM_ENCODER_FAILURE        = 12,
    SXM_INTERNAL_FAILURE       = 13,
    SXM_UNKNOWN_OPERATION      = 14,
    SXM_ABI_MISMATCH           = 15
} sxm_result_code;

/*
 * Terminal completion for a one-shot operation.
 * utf8_result_json is valid only until this function returns; copy it.
 */
typedef void (*sxm_completion_fn)(uint64_t operation_id,
                                  int32_t result_code,
                                  const uint8_t *utf8_result_json,
                                  uint64_t result_length,
                                  void *user_context);

/*
 * Low-rate event stream for persistent subscriptions (recording state,
 * hotkey presses, display reconfiguration). Never carries media data.
 * utf8_event_json is borrowed for the duration of the call.
 */
typedef void (*sxm_event_fn)(uint64_t subscription_id,
                             const uint8_t *utf8_event_json,
                             uint64_t event_length,
                             void *user_context);

uint32_t sxm_abi_version(void);

/* Installs the process-wide event sink. Pass NULL to detach. Idempotent. */
int32_t sxm_set_event_sink(sxm_event_fn sink, void *user_context);

int32_t sxm_begin(const uint8_t *utf8_request_json,
                  uint64_t request_length,
                  sxm_completion_fn completion,
                  void *user_context,
                  uint64_t *out_operation_id);

int32_t sxm_cancel(uint64_t operation_id);
int32_t sxm_release(uint64_t operation_id);

/*
 * Bounded, synchronous accessor for the result buffer of an operation that
 * produced binary output (for example a PNG-encoded screenshot). Returns the
 * number of bytes written, or a negative sxm_result_code on failure.
 * Call with out_buffer == NULL to query the required length.
 */
int64_t sxm_copy_blob(uint64_t operation_id,
                      uint8_t *out_buffer,
                      uint64_t buffer_capacity);

#ifdef __cplusplus
}
#endif

#endif /* SHAREX_MAC_ABI_H */
