//  Beat Saber Custom Avatars - Custom player models for body presence in Beat Saber.
//  Copyright � 2018-2020  Beat Saber Custom Avatars Contributors
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

using CustomAvatar.Tracking;
using System;
using CustomAvatar.Logging;
using UnityEngine;
using Zenject;

namespace CustomAvatar.Avatar
{
    public class AvatarTracking : MonoBehaviour
    {
        private Pose _initialPelvisPose;
        private Pose _initialLeftFootPose;
        private Pose _initialRightFootPose;

        private Vector3 _prevBodyLocalPosition = Vector3.zero;

        public bool isCalibrationModeEnabled = false;

        private IAvatarInput _input;
        private SpawnedAvatar _avatar;
        private ILogger<AvatarTracking> _logger = new UnityDebugLogger<AvatarTracking>();

        #region Behaviour Lifecycle
        #pragma warning disable IDE0051
        // ReSharper disable UnusedMember.Local

        [Inject]
        private void Inject(ILoggerProvider loggerProvider, IAvatarInput input, SpawnedAvatar avatar)
        {
            _logger = loggerProvider.CreateLogger<AvatarTracking>(avatar.avatar.descriptor.name);
            _input = input;
            _avatar = avatar;

            if (_avatar.pelvis)   _initialPelvisPose    = new Pose(_avatar.pelvis.position,   _avatar.pelvis.rotation);
            if (_avatar.leftLeg)  _initialLeftFootPose  = new Pose(_avatar.leftLeg.position,  _avatar.leftLeg.rotation);
            if (_avatar.rightLeg) _initialRightFootPose = new Pose(_avatar.rightLeg.position, _avatar.rightLeg.rotation);
        }

        private void LateUpdate()
        {
            try
            {
                SetPose(DeviceUse.Head, _avatar.head);
                SetPose(DeviceUse.LeftHand, _avatar.leftHand);
                SetPose(DeviceUse.RightHand, _avatar.rightHand);

                if (isCalibrationModeEnabled)
                {
                    // TODO fix all of this
                    if (_avatar.pelvis)
                    {
                        _avatar.pelvis.position = _initialPelvisPose.position;// * _avatar.scale + new Vector3(0, _avatar.verticalPosition, 0);
                        _avatar.pelvis.rotation = _initialPelvisPose.rotation;
                    }

                    if (_avatar.leftLeg)
                    {
                        _avatar.leftLeg.position = _initialLeftFootPose.position;// * _avatar.scale + new Vector3(0, _avatar.verticalPosition, 0);
                        _avatar.leftLeg.rotation = _initialLeftFootPose.rotation;
                    }

                    if (_avatar.rightLeg)
                    {
                        _avatar.rightLeg.position = _initialRightFootPose.position;// * _avatar.scale + new Vector3(0, _avatar.verticalPosition, 0);
                        _avatar.rightLeg.rotation = _initialRightFootPose.rotation;
                    }
                }
                else
                {
                    SetPose(DeviceUse.Waist, _avatar.pelvis);
                    SetPose(DeviceUse.LeftFoot, _avatar.leftLeg);
                    SetPose(DeviceUse.RightFoot, _avatar.rightLeg);
                }

                if (_avatar.body)
                {
                    _avatar.body.position = _avatar.head.position - (_avatar.head.up * 0.1f);

                    var vel = new Vector3(_avatar.body.localPosition.x - _prevBodyLocalPosition.x, 0.0f,
                        _avatar.body.localPosition.z - _prevBodyLocalPosition.z);

                    var rot = Quaternion.Euler(0.0f, _avatar.head.localEulerAngles.y, 0.0f);
                    var tiltAxis = Vector3.Cross(transform.up, vel);

                    _avatar.body.localRotation = Quaternion.Lerp(_avatar.body.localRotation,
                        Quaternion.AngleAxis(vel.magnitude * 1250.0f, tiltAxis) * rot,
                        Time.deltaTime * 10.0f);

                    _prevBodyLocalPosition = _avatar.body.localPosition;
                }
            }
            catch (Exception e)
            {
                _logger.Error($"{e.Message}\n{e.StackTrace}");
            }
        }

        // ReSharper restore UnusedMember.Local
        #pragma warning restore IDE0051
        #endregion

        private void SetPose(DeviceUse use, Transform target)
        {
            if (!target || !_input.TryGetPose(use, out Pose pose)) return;

            // if avatar transform has a parent, use that as the origin
            if (transform.parent)
            {
                target.position = transform.parent.TransformPoint(pose.position);
                target.rotation = transform.parent.rotation * pose.rotation;
            }
            else
            {
                target.position = pose.position;
                target.rotation = pose.rotation;
            }
        }
    }
}
