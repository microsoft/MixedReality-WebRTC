// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "../include/api.h"

class PCRaii {
 public:
  PCRaii() {
    PeerConnectionConfiguration config {};
	config.encoded_ice_servers = "stun:stun.l.google.com:19302";
    mrsPeerConnectionCreate(config, &handle_);
  }
  ~PCRaii() { mrsPeerConnectionClose(&handle_); }
  PeerConnectionHandle handle() const { return handle_; }

 protected:
  PeerConnectionHandle handle_{};
};
