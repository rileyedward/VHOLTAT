using System.Collections.Generic;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Serializing;
using FishNet.Transporting;
using UnityEngine;
using VHOLTAT.Adapter;
using VHOLTAT.Movement;

namespace VHOLTAT.Net
{
    /// <summary>One player's parkour state, for drawing their model on other screens.</summary>
    internal struct PosePacket : IBroadcast
    {
        public int ObjectId;
        public byte State;
    }

    /// <summary>
    /// Shares each player's VHOLTAT movement state (wall run side, climb, mantle/vault, slide...) so other copies can
    /// pose that player's model (see RemotePoses). Owners send on change and at least twice a second; clients send to
    /// the host, which relays to everyone. Remote players without VHOLTAT simply have no entry.
    /// </summary>
    internal sealed class PoseSync : MonoBehaviour
    {
        private const float Heartbeat = 0.5f;
        private const float MinInterval = 1f / 15f;

        /// <summary>Latest known state per player NetworkObject id, and when it arrived (unscaled time).</summary>
        public static readonly Dictionary<int, KeyValuePair<MoveState, float>> States = new Dictionary<int, KeyValuePair<MoveState, float>>();

        private NetworkManager registeredOn;
        private byte lastSent = 255;
        private float lastSendTime;

        static PoseSync()
        {
            GenericWriter<PosePacket>.Write = (w, p) => { w.WriteInt32(p.ObjectId, AutoPackType.Packed); w.WriteByte(p.State); };
            GenericReader<PosePacket>.Read = r => new PosePacket { ObjectId = r.ReadInt32(AutoPackType.Packed), State = r.ReadByte() };
        }

        private void Update()
        {
            var nm = InstanceFinder.NetworkManager;
            if (nm != null && nm != registeredOn)
            {
                nm.ServerManager.RegisterBroadcast<PosePacket>(OnServer);
                nm.ClientManager.RegisterBroadcast<PosePacket>(OnClient);
                registeredOn = nm;
                States.Clear();
            }
            if (nm == null || !InstanceFinder.IsClient)
                return;

            var fpc = MotorDriver.Current;
            var m = MotorDriver.Motor;
            if (fpc == null || m == null || fpc.NetworkObject == null)
                return;
            byte state = MotorDriver.Active ? (byte)m.State : (byte)MoveState.Ground;
            float now = Time.unscaledTime;
            bool changed = state != lastSent;
            if ((changed && now - lastSendTime >= MinInterval) || now - lastSendTime >= Heartbeat)
            {
                var packet = new PosePacket { ObjectId = fpc.NetworkObject.ObjectId, State = state };
                if (InstanceFinder.IsServer)
                    nm.ServerManager.Broadcast(packet, true, Channel.Unreliable);
                else if (nm.ClientManager.Started)
                    nm.ClientManager.Broadcast(packet, Channel.Unreliable);
                lastSent = state;
                lastSendTime = now;
            }
        }

        /// <summary>Host: relay a client's state to everyone.</summary>
        private void OnServer(NetworkConnection conn, PosePacket packet)
        {
            InstanceFinder.ServerManager.Broadcast(packet, true, Channel.Unreliable);
        }

        private void OnClient(PosePacket packet)
        {
            States[packet.ObjectId] = new KeyValuePair<MoveState, float>((MoveState)packet.State, Time.unscaledTime);
        }
    }
}
