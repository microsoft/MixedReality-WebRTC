// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.MixedReality.WebRTC;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace TestAppUwp
{
    /// <summary>
    /// Convert <c>true</c> to <c>false</c>, and anything else (including <c>false</c>) to <c>true</c>.
    /// The back-conversion is also available, and simply negate the boolean value.
    /// </summary>
    public class BooleanInverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // true          => false
            // anything else => true
            return (!(value is bool boolValue) || !boolValue);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // true  => false
            // false => true
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            throw new ArgumentException("Cannot back-convert generic non-bool object with BooleanInverter.");
        }
    }

    /// <summary>
    /// Adapter to convert a boolean to a <see cref="Visibility"/> value, with inverted meaning
    /// as the default implicit converter:
    /// - <c>true</c> <=> <c>Visibility.Collapsed</c>
    /// - <c>false</c> <=> <c>Visibility.Visible</c>
    ///
    /// Useful for mapping a property <c>IsHidden</c> to the visibility of a control.
    /// </summary>
    public class BooleanToVisibilityInvertedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if ((value is bool boolValue) && boolValue)
            {
                return Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return (!(value is Visibility visibilityValue) || (visibilityValue != Visibility.Visible));
        }
    }

    public class VisibleIfNotEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if ((value is string stringValue) && !string.IsNullOrWhiteSpace(stringValue))
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException("VisibilleIfNotEmptyConverter is a one-way converter by design.");
        }
    }

    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value != null);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException("NullToBoolConverter is a one-way converter by design.");
        }
    }

    public class CountToBoolNotEmptyConvert : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return ((value is int intValue) && (intValue > 0));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException("CountToBoolNotEmptyConvert is a one-way converter by design.");
        }
    }

    /// <summary>
    /// Convert a WebRTC <see cref="MediaKind"/> to a <see cref="Symbol"/> for UI display.
    /// </summary>
    public class MediaKindToSymbolConverter : IValueConverter
    {
        private const Symbol AudioSymbol = Symbol.Volume;
        private const Symbol VideoSymbol = Symbol.Video;

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is MediaKind mediaKind)
            {
                switch (mediaKind)
                {
                case MediaKind.Audio: return AudioSymbol;
                case MediaKind.Video: return VideoSymbol;
                }
            }
            throw new ArgumentOutOfRangeException($"Cannot convert non-MediaKind to Symbol.");
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Symbol symbolValue)
            {
                switch (symbolValue)
                {
                case AudioSymbol: return MediaKind.Audio;
                case VideoSymbol: return MediaKind.Video;
                default: throw new ArgumentOutOfRangeException($"Cannot convert unknown Symbol to WebRTC MediaKind.");
                }
            }
            throw new ArgumentOutOfRangeException($"Cannot convert non-Symbol to WebRTC MediaKind.");
        }
    }

    /// <summary>
    /// Convert a WebRTC <see cref="Transceiver.Direction"/> to a <see cref="Symbol"/> for UI display.
    /// </summary>
    public class TransceiverDirectionToSymbolConverter : IValueConverter
    {
        private const Symbol InactiveSymbol = Symbol.Clear;
        private const Symbol SendOnlySymbol = Symbol.Forward;
        private const Symbol ReceiveOnlySymbol = Symbol.Back;
        private const Symbol SendReceiveSymbol = Symbol.Switch;
        private const Symbol NullSymbol = Symbol.Sync;

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is Transceiver.Direction directionValue)
            {
                switch (directionValue)
                {
                case Transceiver.Direction.Inactive: return InactiveSymbol;
                case Transceiver.Direction.SendOnly: return SendOnlySymbol;
                case Transceiver.Direction.ReceiveOnly: return ReceiveOnlySymbol;
                case Transceiver.Direction.SendReceive: return SendReceiveSymbol;
                default: break;
                }
            }
            else if (value is null)
            {
                return NullSymbol;
            }
            throw new ArgumentOutOfRangeException($"Cannot convert non-Transceiver.Direction to Symbol.");
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Symbol symbolValue)
            {
                switch (symbolValue)
                {
                case InactiveSymbol: return Transceiver.Direction.Inactive;
                case SendOnlySymbol: return Transceiver.Direction.SendOnly;
                case ReceiveOnlySymbol: return Transceiver.Direction.ReceiveOnly;
                case SendReceiveSymbol: return Transceiver.Direction.SendReceive;
                default: throw new ArgumentOutOfRangeException($"Cannot convert unknown Symbol to WebRTC Transceiver.Direction.");
                }
            }
            throw new ArgumentOutOfRangeException($"Cannot convert non-Symbol to WebRTC Transceiver.Direction.");
        }
    }

    public class StringFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (parameter is string stringParameter)
            {
                return string.Format(stringParameter, value);
            }
            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException("StringFormatConverter is a one-way converter by design.");
        }
    }

    public class QualityValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // Unset = -1.
            if (value == null)
            {
                return "-1";
            }

            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            var s = (string)value;
            int parsed;
            try
            {
                parsed = int.Parse(s);
            }
            catch (Exception)
            {
                return null;
            }

            // parameter is the max value for this.
            var max = int.Parse((string)parameter);
            if (parsed < 0 || parsed > max)
            {
                return null;
            }
            return parsed;
        }
    }

    public class ProfileToIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (int)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return (PeerConnection.H264Profile)value;
        }
    }

    public class RcModeToIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // null = unset (0). Bump the other values by 1.
            return value == null ? 0 : ((int)value + 1);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // 0 = unset (null). Offset the other values by -1.
            return ((int)value == 0) ? null : (PeerConnection.H264RcMode?)((int)value - 1);
        }
    }
}
