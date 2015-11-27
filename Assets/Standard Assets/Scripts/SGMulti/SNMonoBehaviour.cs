using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SGMulti
{
    public class SNMonoBehaviour : MonoBehaviour
    {

        /// <summary>
        /// Called when a packet is wanting to be sent, or was recieved.
        /// </summary>
        /// <param name="packet">The packet that wants to be sent.</param>
        /// <param name="isSelf">Is the object owned by us?</param>
        protected virtual void OnSynchronize(Packet packet, bool isSelf)
        {
            if (packet.IsWriteable)
            {
                // Write the position.
                Vector3 position = transform.position;
                packet.Write(position);

                // Write the rotation.
                Quaternion rotation = transform.rotation;
                packet.Write(rotation);
            }
            else if(!isSelf)
            {
                Vector3 position = packet.ReadVector3();

                Quaternion rotation = packet.ReadQuaternion();

                transform.position = position;
                transform.rotation = rotation;
            }
        }

    }
}
