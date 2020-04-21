// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using UnityEngine;

// This file contains a collection of attributes only used for editing purpose,
// generally to customize the Inspector window. They need to be located in the
// runtime assembly to be attached to runtime object fields, but do not influence
// their runtime behavior.

namespace Microsoft.MixedReality.WebRTC.Unity.Editor
{
    /// <summary>
    /// Attribute to display a boolean field with a toggle on its left, prefixing
    /// the actual text of the field.
    /// </summary>
    public class ToggleLeftAttribute : PropertyAttribute
    {
    }
}
