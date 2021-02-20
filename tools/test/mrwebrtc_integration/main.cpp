
#include <cstdio>
#include <mrwebrtc/interop_api.h>
#include <mrwebrtc/ref_counted_object_interop.h>

#define MRS_ENSURE_SUCCESS(x) \
  do { \
    const mrsResult _ret = (x); \
    if (_ret != mrsResult::kSuccess) { \
      printf("Failed with error code 0x%08x.\n", _ret); \
      return 1; \
    } \
    printf("Success: " #x "\n"); \
  } while (0, 0)

int main(int argc, char* argv[]) {
  mrsPeerConnectionConfiguration config{};
  mrsPeerConnectionHandle handle{};
  MRS_ENSURE_SUCCESS(mrsPeerConnectionCreate(&config, &handle));
  MRS_ENSURE_SUCCESS(mrsPeerConnectionClose(handle));
  mrsRefCountedObjectRemoveRef(handle);
  return 0;
}
