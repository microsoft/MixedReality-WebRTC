// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Attribute for string properties representing an SDP token, which has constraints
    /// on the allowed characters it can contain, as defined in the SDP RFC.
    /// See https://tools.ietf.org/html/rfc4566#page-43 for details.
    /// </summary>
    public class SdpTokenAttribute : PropertyAttribute
    {
        /// <summary>
        /// Allow empty tokens. This is not valid in the RFC, but can be allowed to represent
        /// a default value generated at runtime instead of provided by the user.
        /// </summary>
        public bool AllowEmpty { get; }

        public SdpTokenAttribute(bool allowEmpty = true)
        {
            AllowEmpty = allowEmpty;
        }

        /// <summary>
        /// Validate an SDP token name against the list of allowed characters:
        /// - Symbols [!#$%'*+-.^_`{|}~] and ampersand &amp;
        /// - Alphanumerical [A-Za-z0-9]
        /// </summary>
        /// <remarks>
        /// See https://tools.ietf.org/html/rfc4566#page-43 for 'token' reference.
        /// </remarks>
        /// <param name="name">The token name to validate.</param>
        /// <exception cref="System.ArgumentException">The name contains invalid characters.</exception>
        public static void Validate(string name, bool allowEmpty = true)
        {
            if (allowEmpty && (name.Length == 0))
            {
                return;
            }

            var regex = new Regex("^[A-Za-z0-9!#$%&'*+-.^_`{|}~]+$");
            if (regex.IsMatch(name))
            {
                return;
            }

            throw new ArgumentException($"SDP token '{name}' contains invalid characters.");
        }
    }
}