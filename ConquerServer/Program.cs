﻿using System;
using ConquerServer.Network;
using ConquerServer.Network.Sockets;
using ConquerServer.Database;
using ConquerServer.Database.Models;
using System.Reflection;
using System.Data;
using ConquerServer.Client;

namespace ConquerServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            #region testing


            //Db.GetMagicTypeById()

            #endregion

            Console.WriteLine("Loading database...");
            Db.Load();

            Console.Write("Starting auth server... ");
            var auth = new AuthServerSocket(9959);
            auth.ClientConnected += Auth_ClientConnected;
            auth.ClientMessage += Auth_ClientReceive;
            auth.Start();
            Console.WriteLine("OK");

            Console.Write("Starting game server... ");
            var game = new GameServerSocket(5816);
            game.ClientConnected += Game_ClientConnected;
            game.ClientMessage += Game_ClientMessage;
            game.ClientDisconnected += Game_ClientDisconnected;
            game.Start();
            Console.WriteLine("OK");

            for (; ; )
                Console.ReadLine();
        }

        private static void Game_ClientDisconnected(ClientSocket socket)
        {
            // ..
            if (socket.State == null) return;
            var client = socket.State as GameClient;
            if (client == null) return;

            // if this player still exists in the world
            GameClient existing;
            if (client.World.TryGetPlayer(client.Id, out existing) && existing == client)
            {
                // save their data
                client.Database.SaveCharacter();
                // remove them from the world
                client.World.RemovePlayer();

                Console.WriteLine($"{client.Id}:{client.Username} has disconnected");
            }
        }

        private static void Game_ClientMessage(ClientSocket socket, Packet p)
        {
            if (socket.State == null) return;
            var client = (GameClient)socket.State;

            try
            {
                if (!client.DispatchNetwork(p))
                {
                    Console.WriteLine(p.Dump("Unknown Packet"));
                }
            }
            catch (Exception ex)
            {
                client.Disconnect();
                Console.WriteLine("Disconnected {0} because -\n{1}", client.Username, ex.ToString());
            }
        }

        private static void Game_ClientConnected(ClientSocket socket)
        {
            var client = new GameClient(socket);
            socket.State = client;
            Console.WriteLine("Game Client Connected (post-handshake)");
        }

        private static void Auth_ClientConnected(ClientSocket socket)
        {
            var client = new AuthClient(socket);
            socket.State = client;
            Console.WriteLine("Auth Client Connected");
        }

        private static void Auth_ClientReceive(ClientSocket socket, Packet p)
        {
            if (socket.State == null) return;

            var client = (AuthClient)socket.State;
            client.Process(p);
        }
    }
}