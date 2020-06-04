// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using UnityEngine;

/// <summary>
/// Simple flyover rotating the camera around its parent.
/// </summary>
public class CameraFlyover : MonoBehaviour
{
    /// <summary>
    /// Distance from target object (parent transform) origin.
    /// </summary>
    [Tooltip("Distance from parent object")]
    public float Distance = 1.5f;

    /// <summary>
    /// Altitude over horizon of target object (parent transform), in degrees.
    /// </summary>
    [Tooltip("Flyout altitude in degrees over horizon of parent object")]
    public float Altitude = 0f;

    /// <summary>
    /// Rotation speed in degrees per second around the target object (parent
    /// transform) at the configured altitude. The transform of the owner
    /// GameObject will be modified to oscillate between -45 degrees and +45
    /// degrees at the given speed.
    /// </summary>
    [Tooltip("Azimut rotation around parent object in degrees per second")]
    public float AzimutRotateSpeed = 20f;

    /// <summary>
    /// Current azimut, in degrees.
    /// </summary>
    private float _azimut = 0f;

    /// <summary>
    /// Sign (+/-1) representing the direction in which the azimut is accumualated.
    /// This automatically reverses when reaches the +/-45 degrees limits.
    /// </summary>
    private float _azimutSign = 1f;

    void Update()
    {
        // Smooth the speed to reduce the bumpy effects on +/-45 degrees limits
        float smoothSpeed = AzimutRotateSpeed * (1.0f - Mathf.Sin(Mathf.Abs(_azimut) / 180f * Mathf.PI));

        // Calculate the new azimut
        _azimut += _azimutSign * smoothSpeed * Time.deltaTime;
        if (_azimut > 45f)
        {
            _azimutSign = -1f;
        }
        else if (_azimut < -45f)
        {
            _azimutSign = +1f;
        }

        // Update the transform (relative to the parent transform)
        Quaternion rotation = Quaternion.Euler(Altitude, _azimut, 0f);
        transform.localRotation = rotation;
        transform.localPosition = rotation * (Vector3.back * Distance);
    }
}
