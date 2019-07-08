// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

/// <summary>
/// Simple helper component to toggle the enabled state of another component.
/// </summary>
public class ToggleEnabled : MonoBehaviour
{
    public MonoBehaviour ToggleTarget;

    public void Toggle()
    {
        ToggleTarget.enabled = !ToggleTarget.enabled;
    }
}
