﻿//  Beat Saber Custom Avatars - Custom player models for body presence in Beat Saber.
//  Copyright © 2018-2020  Beat Saber Custom Avatars Contributors
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <https://www.gnu.org/licenses/>.

using CustomAvatar.Logging;
using System;
using UnityEngine;
using Valve.VR;
using Zenject;

namespace CustomAvatar.Tracking.OpenVR
{
    internal class OpenVRDeviceManager : ITrackedDeviceManager, ITickable
    {
        public ITrackedDeviceState head      => _head;
        public ITrackedDeviceState leftHand  => _leftHand;
        public ITrackedDeviceState rightHand => _rightHand;
        public ITrackedDeviceState waist     => _waist;
        public ITrackedDeviceState leftFoot  => _leftFoot;
        public ITrackedDeviceState rightFoot => _rightFoot;

        public event Action<ITrackedDeviceState> deviceAdded;
        public event Action<ITrackedDeviceState> deviceRemoved;
        public event Action<ITrackedDeviceState> deviceTrackingAcquired;
        public event Action<ITrackedDeviceState> deviceTrackingLost;

        private readonly OpenVRDeviceState _head      = new OpenVRDeviceState(DeviceUse.Head);
        private readonly OpenVRDeviceState _leftHand  = new OpenVRDeviceState(DeviceUse.LeftHand);
        private readonly OpenVRDeviceState _rightHand = new OpenVRDeviceState(DeviceUse.RightHand);
        private readonly OpenVRDeviceState _waist     = new OpenVRDeviceState(DeviceUse.Waist);
        private readonly OpenVRDeviceState _leftFoot  = new OpenVRDeviceState(DeviceUse.LeftFoot);
        private readonly OpenVRDeviceState _rightFoot = new OpenVRDeviceState(DeviceUse.RightFoot);

        private ILogger<OpenVRDeviceManager> _logger;
        private OpenVRFacade _openVRFacade;

        private readonly bool[] _connectedDevices = new bool[OpenVRFacade.kMaxTrackedDeviceCount];
        private readonly string[] _roles = new string[OpenVRFacade.kMaxTrackedDeviceCount];
        private readonly TrackedDevicePose_t[] _poses = new TrackedDevicePose_t[OpenVRFacade.kMaxTrackedDeviceCount];

        public OpenVRDeviceManager(ILoggerProvider loggerProvider, OpenVRFacade openVRFacade)
        {
            _logger = loggerProvider.CreateLogger<OpenVRDeviceManager>();
            _openVRFacade = openVRFacade;
        }

        public void Tick()
        {
            _openVRFacade.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, _poses);

            for (uint i = 0; i < OpenVRFacade.kMaxTrackedDeviceCount; i++)
            {
                string role = _openVRFacade.GetStringTrackedDeviceProperty(i, ETrackedDeviceProperty.Prop_ControllerType_String);

                if (_poses[i].bDeviceIsConnected != _connectedDevices[i] || _roles[i] != role)
                {
                    UpdateDevice(i, _poses[i].bDeviceIsConnected, role);
                }
            }

            UpdateDeviceState(_head);
            UpdateDeviceState(_leftHand);
            UpdateDeviceState(_rightHand);
            UpdateDeviceState(_waist);
            UpdateDeviceState(_leftFoot);
            UpdateDeviceState(_rightFoot);
        }

        private void UpdateDevice(uint deviceIndex, bool connected, string role)
        {
            string modelName = _openVRFacade.GetStringTrackedDeviceProperty(deviceIndex, ETrackedDeviceProperty.Prop_ModelNumber_String);
            string serialNumber = _openVRFacade.GetStringTrackedDeviceProperty(deviceIndex, ETrackedDeviceProperty.Prop_SerialNumber_String);

            if (_connectedDevices[deviceIndex] != connected)
            {
                if (connected)
                {
                    _logger.Info($"Device '{modelName}' (S/N '{serialNumber}') connected at index {deviceIndex}");
                }
                else
                {
                    _logger.Info($"Device '{modelName}' (S/N '{serialNumber}') disconnected at index {deviceIndex}");
                    return;
                }
            }

            _logger.Trace($"Device {deviceIndex} has role '{role}'");

            if (!string.IsNullOrEmpty(_roles[deviceIndex]) && _roles[deviceIndex] != role)
            {
                _logger.Info($"Device {deviceIndex} changed roles from '{_roles[deviceIndex]}' to '{role}'");
            }

            _connectedDevices[deviceIndex] = connected;
            _roles[deviceIndex] = role;

            ETrackedDeviceClass deviceClass = (ETrackedDeviceClass) _openVRFacade.GetInt32TrackedDeviceProperty(deviceIndex, ETrackedDeviceProperty.Prop_DeviceClass_Int32);

            _logger.Trace($"Device {deviceIndex} has class '{deviceClass}'");
            
            switch (deviceClass)
            {
                case ETrackedDeviceClass.HMD:
                    AssignTrackedDevice(_head, deviceIndex, modelName, serialNumber, role);
                    break;

                case ETrackedDeviceClass.Controller:
                    ETrackedControllerRole hand = _openVRFacade.GetControllerRoleForTrackedDeviceIndex(deviceIndex);

                    switch (hand)
                    {
                        case ETrackedControllerRole.LeftHand:
                            AssignTrackedDevice(_leftHand, deviceIndex, modelName, serialNumber, role);
                            break;

                        case ETrackedControllerRole.RightHand:
                            AssignTrackedDevice(_rightHand, deviceIndex, modelName, serialNumber, role);
                            break;
                    }

                    break;

                case ETrackedDeviceClass.GenericTracker:
                    switch (role)
                    {
                        case "vive_tracker_waist":
                            AssignTrackedDevice(_waist, deviceIndex, modelName, serialNumber, role);
                            break;

                        case "vive_tracker_left_foot":
                            AssignTrackedDevice(_leftFoot, deviceIndex, modelName, serialNumber, role);
                            break;

                        case "vive_tracker_right_foot":
                            AssignTrackedDevice(_rightFoot, deviceIndex, modelName, serialNumber, role);
                            break;
                    }

                    break;
            }
        }

        private void AssignTrackedDevice(OpenVRDeviceState deviceState, uint deviceIndex, string modelName, string serialNumber, string role)
        {
            if (deviceState.isConnected && deviceState.deviceIndex != deviceIndex)
            {
                _logger.Info($"Deassigning device '{deviceState.modelName}' (S/N '{deviceState.serialNumber}') from '{deviceState.use}'");
            }

            deviceState.deviceIndex = deviceIndex;
            deviceState.modelName = modelName;
            deviceState.serialNumber = serialNumber;
            deviceState.role = role;
            deviceState.isConnected = true;

            _logger.Info($"Assigned device '{deviceState.modelName}' (S/N '{deviceState.serialNumber}') to '{deviceState.use}'");

            deviceAdded?.Invoke(deviceState);
        }

        private void UpdateDeviceState(OpenVRDeviceState deviceState)
        {
            if (!deviceState.isConnected) return;

            if (!_connectedDevices[deviceState.deviceIndex] || deviceState.role != _roles[deviceState.deviceIndex])
            {
                _logger.Info($"Lost device '{deviceState.modelName}' (S/N '{deviceState.serialNumber}') used as '{deviceState.use}'");

                deviceRemoved?.Invoke(deviceState);

                deviceState.deviceIndex = 0;
                deviceState.modelName = null;
                deviceState.serialNumber = null;
                deviceState.position = Vector3.zero;
                deviceState.rotation = Quaternion.identity;
                deviceState.isConnected = false;
                deviceState.isTracking = false;

                return;
            }

            TrackedDevicePose_t pose = _poses[deviceState.deviceIndex];

            bool isTracking = pose.bPoseIsValid && pose.eTrackingResult == ETrackingResult.Running_OK;

            if (deviceState.isTracking != isTracking)
            {
                if (isTracking)
                {
                    _logger.Info($"Acquired tracking of device '{deviceState.modelName}' (S/N '{deviceState.serialNumber}')");
                    deviceTrackingAcquired?.Invoke(deviceState);
                }
                else
                {
                    _logger.Info($"Lost tracking of device '{deviceState.modelName}' (S/N '{deviceState.serialNumber}')");
                    deviceTrackingLost?.Invoke(deviceState);
                }
            }

            deviceState.isTracking = isTracking;

            if (!isTracking) return;

            Vector3 position = _openVRFacade.GetPosition(pose.mDeviceToAbsoluteTracking);
            Quaternion rotation = _openVRFacade.GetRotation(pose.mDeviceToAbsoluteTracking);

            // Driver4VR rotation correction
            if (deviceState.role.StartsWith("d4vr_tracker_") && (deviceState.use == DeviceUse.LeftFoot || deviceState.use == DeviceUse.RightFoot))
            {
                rotation *= Quaternion.Euler(-90, 180, 0);
            }

            // KinectToVR rotation correction
            if (deviceState.role == "kinect_device")
            {
                if (deviceState.use == DeviceUse.Waist)
                {
                    rotation *= Quaternion.Euler(-90, 180, 0);
                }

                if (deviceState.use == DeviceUse.LeftFoot || deviceState.use == DeviceUse.RightFoot)
                {
                    rotation *= Quaternion.Euler(0, 180, 0);
                }
            }

            deviceState.position = position;
            deviceState.rotation = rotation;
        }
    }
}
