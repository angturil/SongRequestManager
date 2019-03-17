using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using VRUIControls;

namespace EnhancedTwitchChat.Chat
{
    class ChatMover : MonoBehaviour
    {
        protected Transform _moverCube;
        protected VRController _grabbingController = null;
        protected Vector3 _grabPos;
        protected Quaternion _grabRot;
        protected Vector3 _realPos;
        protected Quaternion _realRot;
        protected VRPointer _vrPointer;
        protected bool _wasMoving = false;

        protected const float MinScrollDistance = 0.25f;
        protected const float MaxLaserDistance = 50;
        
        public void Init(Transform moverCube)
        {
            _moverCube = moverCube;
            _vrPointer = GetComponent<VRPointer>();
        }

        // This code was straight copied from xyonico's camera+ mod, so all credit goes to him :)
        public void Update()
        {
            //Plugin.Log($"ChatMover! VRPointer: {(_vrPointer != null ? "NOT NULL" : "NULL")}");
            if (ChatConfig.Instance.LockChatPosition) return;
            if (_vrPointer.vrController != null)
            {
                if (_vrPointer.vrController.triggerValue > 0.9f)
                {
                    if (_grabbingController != null) return;
                    if (Physics.Raycast(_vrPointer.vrController.transform.position, _vrPointer.vrController.forward, out var hit, MaxLaserDistance))
                    {
                        if (hit.transform != _moverCube) return;
                        _grabbingController = _vrPointer.vrController;
                        _grabPos = _vrPointer.vrController.transform.InverseTransformPoint(ChatConfig.Instance.ChatPosition);
                        _grabRot = Quaternion.Inverse(_vrPointer.vrController.transform.rotation) * _moverCube.rotation;
                    }
                }
            }
            if (_grabbingController == null || !(_grabbingController.triggerValue <= 0.9f)) return;
            _grabbingController = null;
        }

        public void LateUpdate()
        {
            if (ChatConfig.Instance.LockChatPosition) return;
            if (_grabbingController != null)
            {
                _wasMoving = true;
                var diff = _grabbingController.verticalAxisValue * Time.deltaTime;
                if (_grabPos.magnitude > MinScrollDistance)
                    _grabPos -= Vector3.forward * diff;
                else
                    _grabPos -= Vector3.forward * Mathf.Clamp(diff, float.MinValue, 0);

                _realPos = _grabbingController.transform.TransformPoint(_grabPos);
                _realRot = _grabbingController.transform.rotation * _grabRot;

                ChatConfig.Instance.ChatPosition = Vector3.Lerp(ChatConfig.Instance.ChatPosition, _realPos, 10 * Time.deltaTime);
                ChatConfig.Instance.ChatRotation = Quaternion.Slerp(_moverCube.rotation, _realRot, 5 * Time.deltaTime).eulerAngles;
            }
            else if (_wasMoving)
            {
                ChatConfig.Instance.Save();
                _wasMoving = false;
            }
        }
    };
}
