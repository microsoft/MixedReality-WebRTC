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
    ///
    /// See https://tools.ietf.org/html/rfc4566#page-43 for details.
    /// </summary>
    public class SdpTokenAttribute : PropertyAttribute
    {
        /// <summary>
        /// Allow empty tokens, that is a string property which is <c>null</c> or an empty string.
        /// This is not valid in the RFC, but can be allowed as a property value to represent a default
        /// value generated at runtime by the implementation instead of being provided by the user.
        /// 
        /// This is typically used as an argument to <see cref="Validate(string, bool)"/>.
        /// </summary>
        /// <value><c>true</c> to allow the property to be <c>null</c> or empty.</value>
        public bool AllowEmpty { get; }

        /// <param name="allowEmpty">Value of <see cref="AllowEmpty"/>.</param>
        public SdpTokenAttribute(bool allowEmpty = true)
        {
            AllowEmpty = allowEmpty;
        }

        /// <summary>
        /// Validate an SDP token name against the list of allowed characters:
        /// - Symbols <c>[!#$%'*+-.^_`{|}~&amp;]</c>
        /// - Alphanumerical characters <c>[A-Za-z0-9]</c>
        /// 
        /// If the validation fails, the method throws an exception.
        /// </summary>
        /// <remarks>
        /// See https://tools.ietf.org/html/rfc4566#page-43 for 'token' reference.
        /// </remarks>
        /// <param name="name">The token name to validate.</param>
        /// <param name="allowEmpty">
        /// <c>true</c> to allow the property to be <c>null</c> or empty without raising an exception.
        /// </param>
        /// <exception xref="System.ArgumentNullException">
        /// <paramref name="name"/> is <c>null</c> or empty, and <see cref="AllowEmpty"/> is <c>false</c>.
        /// </exception>
        /// <exception xref="System.ArgumentException">
        /// <paramref name="name"/> contains invalid characters not allowed for a SDP 'token' item.
        /// </exception>
        public static void Validate(string name, bool allowEmpty = true)
        {
            if (string.IsNullOrEmpty(name))
            {
                if (allowEmpty)
                {
                    return;
                }
                throw new ArgumentNullException("Invalid null SDP token.");
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
