/*
 * ShareX-Mac native bridge — Objective-C shim implementing the public C ABI.
 *
 * The exported symbols live here rather than in Swift so that the ABI does not
 * depend on an underscored Swift export attribute (PROJECT-SPEC.md section 4).
 * Swift implements the behaviour behind @objc classes; this file owns operation
 * id allocation and the translation between C function pointers and blocks.
 */
#import <Foundation/Foundation.h>
#import <stdatomic.h>

#import "sxm_abi.h"
#import "ShareXMacNative-Swift.h"

static _Atomic uint64_t g_next_operation_id = 1;

uint32_t sxm_abi_version(void)
{
    return SXM_ABI_VERSION;
}

int32_t sxm_set_event_sink(sxm_event_fn sink, void *user_context)
{
    if (sink == NULL) {
        [SXMDispatcher.shared setEventSink:nil];
        return SXM_OK;
    }

    [SXMDispatcher.shared setEventSink:^(uint64_t subscriptionId, NSData * _Nullable payload) {
        const uint8_t *bytes = payload ? (const uint8_t *)payload.bytes : NULL;
        uint64_t length = payload ? (uint64_t)payload.length : 0;
        sink(subscriptionId, bytes, length, user_context);
    }];
    return SXM_OK;
}

int32_t sxm_begin(const uint8_t *utf8_request_json,
                  uint64_t request_length,
                  sxm_completion_fn completion,
                  void *user_context,
                  uint64_t *out_operation_id)
{
    if (utf8_request_json == NULL || request_length == 0 || completion == NULL ||
        out_operation_id == NULL) {
        return SXM_INVALID_INPUT;
    }
    /* Bound the request; native side never allocates unbounded managed input. */
    if (request_length > (uint64_t)(8 * 1024 * 1024)) {
        return SXM_INVALID_INPUT;
    }

    *out_operation_id = 0;

    /* Copy before returning, as documented in the ABI contract. */
    NSData *request = [NSData dataWithBytes:utf8_request_json
                                     length:(NSUInteger)request_length];

    uint64_t operationId = atomic_fetch_add(&g_next_operation_id, 1);

    __block BOOL completed = NO;
    void (^sink)(int32_t, NSData * _Nullable) = ^(int32_t code, NSData * _Nullable payload) {
        /* Defensive: the ABI promises at-most-once completion. */
        @synchronized (request) {
            if (completed) { return; }
            completed = YES;
        }
        const uint8_t *bytes = payload ? (const uint8_t *)payload.bytes : NULL;
        uint64_t length = payload ? (uint64_t)payload.length : 0;
        completion(operationId, code, bytes, length, user_context);
    };

    int32_t immediate = [SXMDispatcher.shared beginWithRequest:request
                                                   operationId:operationId
                                                    completion:sink];
    if (immediate != SXM_OK) {
        /* Immediate failure: no callback is delivered, per the ABI contract. */
        return immediate;
    }

    *out_operation_id = operationId;
    return SXM_OK;
}

int32_t sxm_cancel(uint64_t operation_id)
{
    if (operation_id == 0) { return SXM_INVALID_INPUT; }
    return [SXMDispatcher.shared cancelWithOperationId:operation_id];
}

int32_t sxm_release(uint64_t operation_id)
{
    if (operation_id == 0) { return SXM_INVALID_INPUT; }
    return [SXMDispatcher.shared disposeOperationWithId:operation_id];
}

int64_t sxm_copy_blob(uint64_t operation_id, uint8_t *out_buffer, uint64_t buffer_capacity)
{
    if (operation_id == 0) { return -SXM_INVALID_INPUT; }

    NSData *blob = [SXMDispatcher.shared blobForOperationId:operation_id];
    if (blob == nil) { return -SXM_TARGET_DISAPPEARED; }

    if (out_buffer == NULL) { return (int64_t)blob.length; }
    if (buffer_capacity < (uint64_t)blob.length) { return -SXM_INVALID_INPUT; }

    memcpy(out_buffer, blob.bytes, blob.length);
    return (int64_t)blob.length;
}
