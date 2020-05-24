﻿using LiteNetLib;
using LiteNetLib.Utils;

namespace Davinet
{
    public class Peer
    {
        private Remote remote;
        private NetManager netManager;
        private NetDataWriter netDataWriter;
        private EventBasedNetListener listener;

        private enum Role { Inactive, Server, Client, ListenClient }
        private Role role;

        private PeerDebug debug;

        public Peer() : this(null)
        {
        }

        public Peer(PeerDebug debug)
        {
            this.debug = debug;

            listener = new EventBasedNetListener();
            listener.NetworkReceiveEvent += Listener_NetworkReceiveEvent;
            listener.ConnectionRequestEvent += Listener_ConnectionRequestEvent;            

            netManager = new NetManager(listener);            

            if (debug != null)
            {
                netManager.SimulatePacketLoss = debug.simulatePacketLoss;
                netManager.SimulationPacketLossChance = debug.packetLossChance;
            }

            netDataWriter = new NetDataWriter();
        }

        // TODO: Would be nice if server specific logic lived somewhere else.
        private void Listener_PeerConnectedEvent(NetPeer peer)
        {
            int id = remote.AddPeer(peer);

            NetDataWriter writer = new NetDataWriter();
            writer.Put((byte)PacketType.Join);
            writer.Put(id);
            writer.Put(StatefulWorld.Instance.Frame);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
            writer.Reset();

            remote.SynchronizeAll();

            // TODO: This should be part of the gameplay logic layer, since none of it is network specific.
            // Instead, the gameplay layer should listen for when a player joins, and spawn an appropriate prefab.
            var player = UnityEngine.Object.Instantiate(StatefulWorld.Instance.registeredPrefabsMap[1717083505]);
            StatefulWorld.Instance.Add(player.GetComponent<StatefulObject>());
            StatefulWorld.Instance.SetOwnership(player.GetComponent<OwnableObject>(), id);

            UnityEngine.Debug.Log($"Peer {peer.Id} connected.");
        }

        private void Listener_ConnectionRequestEvent(ConnectionRequest request)
        {            
            request.Accept();

            UnityEngine.Debug.Log($"Connection requested.");
        }

        private void Listener_NetworkReceiveEvent(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            PacketType packetType = (PacketType)reader.GetByte();

            if (packetType == PacketType.State)
            {
                ReadState(reader);
            }
            // TODO: This is also server only logic.
            else if (packetType == PacketType.Join)
            {
                int remoteID = reader.GetInt();
                int frame = reader.GetInt();

                StatefulWorld.Instance.Frame = frame;
                remote = new Remote(StatefulWorld.Instance, false, role == Role.ListenClient, remoteID);

                UnityEngine.Debug.Log($"Client assigned id {remoteID}");
            }
        }

        private void ReadState(NetPacketReader reader)
        {
            if (debug != null && debug.simulateLatency)
            {

            }
            else
            {
                int frame = reader.GetInt();
                remote.ReadState(reader);
            }
        }

        public void Listen(int port)
        {
            if (role != Role.Inactive)
                throw new System.Exception($"Cannot start listening with peer already performing the {role} role.");

            listener.PeerConnectedEvent += Listener_PeerConnectedEvent;
            netManager.Start(port);

            remote = new Remote(StatefulWorld.Instance, true, false, 0);

            role = Role.Server;
        }

        public void Connect(string address, int port, bool listenClient=false)
        {
            if (role != Role.Inactive)
                throw new System.Exception($"Cannot connect with peer already performing the {role} role.");

            netManager.Start();
            netManager.Connect(address, port, "Davinet");

            role = listenClient ? Role.ListenClient : Role.Client;
        }

        public void PollEvents()
        {
            netManager.PollEvents();
        }

        public void SendState()
        {
            if (remote != null && role != Role.ListenClient)
            {
                remote.WriteState(netDataWriter);
                netManager.SendToAll(netDataWriter, DeliveryMethod.ReliableUnordered);
                netDataWriter.Reset();
            }
        }
    }
}