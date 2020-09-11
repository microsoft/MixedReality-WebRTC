// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <memory>
#include <vector>

/// The HandlePool class generates opaque handles for objects. Freed
/// handles are reused, but have protection against stale handles
/// referencing new objects occupying a recycled slot.
///
/// Handles are a 32-bit value. Format is:
///  High 16 bits: generation
///  Low 16 bits : slot

template <typename ObjT>
class HandlePool {
 private:
  std::vector<std::shared_ptr<ObjT>> m_instances;
  std::vector<short> m_generations;
  std::vector<short> m_freeSlots;

 public:
  /// Creates a new handle and associates it with the provided object.
  void* bind(std::shared_ptr<ObjT> obj) noexcept;
  /// Unassociates the object and frees the handle.
  std::shared_ptr<ObjT> unbind(void* handle) noexcept;
  /// Gets the object associated with the handle.
  std::shared_ptr<ObjT> get(void* handle) noexcept;
};

template <typename ObjT>
void* HandlePool<ObjT>::bind(std::shared_ptr<ObjT> obj) noexcept {
  if (m_instances.size() > 0xffff && m_freeSlots.size() == 0) {
    // All slots are in use.
    return nullptr;
  }

  short slot;
  if (m_freeSlots.size()) {
    // Use a free slot.
    slot = m_freeSlots.back();
    m_freeSlots.pop_back();
  } else {
    // Allocate a new slot.
    slot = (short)m_instances.size();
    m_instances.resize(m_instances.size() + 1);
  }

  // Store the object in the slot.
  m_instances[slot] = obj;

  // Increment the generation of this slot. This is a guard against stale
  // handles pointing to newer instances occupying a recycled slot.
  m_generations.resize(m_instances.size());
  m_generations[slot] = (m_generations[slot] + 1) & 0xffff;
  // Generation cannot be zero.
  if (!m_generations[slot]) {
    ++m_generations[slot];
  }
  short gen = m_generations[slot];
  intptr_t handleVal = ((gen << 16) | slot);
  void* handle = (void*)handleVal;

  return handle;
}

template <typename ObjT>
std::shared_ptr<ObjT> HandlePool<ObjT>::unbind(void* handle) noexcept {
  std::shared_ptr<ObjT> obj;
  short slot = (intptr_t)handle & 0xffff;
  short gen = ((intptr_t)handle >> 16) & 0xffff;
  if (m_generations.size() > slot && m_generations[slot] == gen) {
    obj = m_instances[slot];
    m_instances[slot] = nullptr;
    m_freeSlots.push_back(slot);
  }
  return obj;
}

template <typename ObjT>
std::shared_ptr<ObjT> HandlePool<ObjT>::get(void* handle) noexcept {
  std::shared_ptr<ObjT> obj;
  short slot = (intptr_t)handle & 0xffff;
  short gen = ((intptr_t)handle >> 16) & 0xffff;
  if (m_generations.size() > slot && m_generations[slot] == gen) {
    obj = m_instances[slot];
  }
  return obj;
}
