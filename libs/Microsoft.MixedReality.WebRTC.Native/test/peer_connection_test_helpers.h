// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "../include/api.h"

class PCRaii {
 public:
  PCRaii() {
    constexpr const char stunServerUrl[] = "stun:stun.l.google.com:19302";
    const char* stunServer = stunServerUrl;
    handle_ = mrsPeerConnectionCreate(&stunServer, 1, "", "", false);
  }
  ~PCRaii() { mrsPeerConnectionClose(&handle_); }
  PeerConnectionHandle handle() const { return handle_; }

 protected:
  PeerConnectionHandle handle_{};
};
