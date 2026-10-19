using System.IO;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Serializing;
using FishNet.Transporting;
using UnityEngine;

namespace VHOLTAT.Net
{
    /// <summary>The single FishNet broadcast VHOLTAT uses. Plugins aren't processed by FishNet's code generator, so
    /// the serializer is registered by hand.</summary>
    internal struct VholtatPacket : IBroadcast
    {
        public byte[] Data;
    }

    /// <summary>
    /// Host-authoritative movement settings. The host sends its preset and every movement number to all clients every
    /// two seconds (a heartbeat, so late joiners and dropped packets catch up); clients with UseHostSettings apply
    /// them on top of their own config until they leave. Everyone already runs the same VHOLTAT version because
    /// STRAFTAT's matchmaking key includes it.
    /// </summary>
    internal sealed class SettingsSync : MonoBehaviour
    {
        private const byte MsgSettings = 1;
        private const float Interval = 2f;

        private NetworkManager registeredOn;
        private float nextSend;
        private readonly MemoryStream ms = new MemoryStream(1024);

        static SettingsSync()
        {
            GenericWriter<VholtatPacket>.Write = (w, p) =>
            {
                int n = p.Data?.Length ?? 0;
                w.WriteInt32(n, AutoPackType.Unpacked);
                if (n > 0) w.WriteBytes(p.Data, 0, n);
            };
            GenericReader<VholtatPacket>.Read = r =>
            {
                int n = r.ReadInt32(AutoPackType.Unpacked);
                return new VholtatPacket { Data = n > 0 ? r.ReadBytesAllocated(n) : new byte[0] };
            };
        }

        private void Update()
        {
            var nm = InstanceFinder.NetworkManager;
            if (nm != null && nm != registeredOn)
            {
                // STRAFTAT recreates its NetworkManager when leaving a lobby or returning to the menu.
                nm.ServerManager.RegisterBroadcast<VholtatPacket>(OnServerPacket);
                nm.ClientManager.RegisterBroadcast<VholtatPacket>(OnClientPacket);
                registeredOn = nm;
            }

            var cfg = Plugin.Settings;
            bool remoteClient = nm != null && InstanceFinder.IsClient && !InstanceFinder.IsServer;
            if (!remoteClient || !cfg.UseHostSettings.Value)
                cfg.ClearRemote();

            if (nm != null && InstanceFinder.IsServer && nm.ServerManager.Clients.Count > 1 && Time.unscaledTime >= nextSend)
            {
                nextSend = Time.unscaledTime + Interval;
                ms.SetLength(0);
                var w = new BinaryWriter(ms);
                w.Write(MsgSettings);
                w.Write(VersionCheck.LocalVersion);
                cfg.WriteLocal(w);
                w.Flush();
                nm.ServerManager.Broadcast(new VholtatPacket { Data = ms.ToArray() }, true, Channel.Reliable);
            }
        }

        private void OnServerPacket(NetworkConnection conn, VholtatPacket packet)
        {
            // Clients don't send anything yet.
        }

        private void OnClientPacket(VholtatPacket packet)
        {
            if (InstanceFinder.IsServer || packet.Data == null || packet.Data.Length < 2)
                return; // the host already uses its own settings
            var cfg = Plugin.Settings;
            using (var r = new BinaryReader(new MemoryStream(packet.Data)))
            {
                if (r.ReadByte() != MsgSettings)
                    return;
                // The version leads every packet, so a mismatch is caught before anything else is read.
                if (!VersionCheck.CheckHost(r.ReadString()) || !cfg.UseHostSettings.Value)
                    return;
                bool first = !cfg.RemoteActive;
                if (!cfg.ApplyRemote(r))
                    Plugin.Logger.LogWarning("host's VHOLTAT settings don't match this version; keeping local settings");
                else if (first)
                    Plugin.Logger.LogInfo($"using the host's VHOLTAT settings (preset {cfg.Movement.Playtest2Model})");
            }
        }
    }
}
