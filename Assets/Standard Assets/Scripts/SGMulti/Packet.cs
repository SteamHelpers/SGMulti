using UnityEngine;
using System.Collections;
using Steamworks;
using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;

using System.Text;
using System.Reflection;

namespace SGMulti
{
	
    public class Packet : IDisposable
    {

        public const long PACKET_DOAUTH = 0xFAFA;
        public const long PACKET_AUTHFAILED = 0xFAFB;
        public const long PACKET_AUTHSUCCESS = 0xFAFC;
		public const long PACKET_USERDISCONNECTED = 0xFBFA;
		public const long PACKET_USERKICKED = 0xFBFB;
        public const long PACKET_SESSIONINFO = 0xFCFA;
        public const long PACKET_UNITYINSTANTIATE = 0x0011;
        public const long PACKET_UNITYOBJECTINFO = 0x0012;

        public const long PACKET_INVALID = 0x0000;
        public const long PACKET_INTRODUCTION = 0x0101;

        private GServer _Server;
        private GClient _Client;
        private bool _IsWriteable;

        /// <summary>
        ///	The type of packet this is, you can use the predefined packet longs or 
        /// </summary>
        public long PacketID
        {
            get;
            private set;
        }

        internal Packet(GServer server, bool iswriteable = true, long packetID = 0x000000)
        {
            PacketID = packetID;
            _Server = server;
            _IsWriteable = iswriteable;

            _Stream = new MemoryStream();
            _Writer = new BinaryWriter(_Stream);
            _Reader = new BinaryReader(_Stream);

            if (_IsWriteable)
            {
                Write(packetID);
            }
        }
		
		void ReadPacketID(){
			if(!_IsWriteable){
				PacketID = ReadLong();
			}
		} 

        internal Packet(GClient client, bool iswriteable = true, long packetID = 0x000000)
        {
            PacketID = packetID;
            _Client = client;
            _IsWriteable = iswriteable;

            _Stream = new MemoryStream();
            _Writer = new BinaryWriter(_Stream);
            _Reader = new BinaryReader(_Stream);

            if (_IsWriteable)
            {
                Write(packetID);
            }
        }

        private BinaryWriter _Writer;
        private BinaryReader _Reader;

        private MemoryStream _Stream;

        public byte[] Bytes
        {
            get { return _Stream.ToArray(); }
            set
            {
                TestReadable();
                _Stream = new MemoryStream(value);
				_Writer = new BinaryWriter(_Stream);
				_Reader = new BinaryReader(_Stream);
				ReadPacketID();
            }
        }

        public bool IsWriteable
        {
            get { return _IsWriteable; }
            private set { _IsWriteable = value; }
        }

        public bool Disposed
        {
            get;
            private set;
        }

        #region Test Methods

        void TestValid()
        {
            if (_Client == null && _Server == null)
                throw new InvalidOperationException("This is an invalid packet object.");
        }

        void TestDisposed()
        {
            if (Disposed)
                throw new InvalidOperationException("Cannot send a packet when the packet is disposed.");
        }

        void TestWriteable()
        {
            TestValid();
            TestDisposed();
            if (!IsWriteable)
                throw new InvalidOperationException("Write-only Packet Error");
        }

        void TestReadable()
        {
            TestValid();
            TestDisposed();
            if (IsWriteable)
                throw new InvalidOperationException("Read-only Packet Error");
        }

        #endregion

        #region Write Methods

        public void Write(Vector3 vector)
        {
            TestWriteable();
            Write(vector.x);
            Write(vector.y);
            Write(vector.z);
        }

        public void Write(Quaternion rotation)
        {
            TestWriteable();
            Write(rotation.w);
            Write(rotation.x);
            Write(rotation.y);
            Write(rotation.z);
        }

        public void Write(byte b)
        {
            TestWriteable();
            _Writer.Write(b);
        }

        public void Write(decimal deci)
        {
            TestWriteable();
            _Writer.Write(deci);
        }

        public void Write(char character)
        {
            TestWriteable();
            _Writer.Write(character);
        }

        public void Write(char[] chararray)
        {
            TestWriteable();
            _Writer.Write(chararray);
        }

        public void Write(bool boolean)
        {
            TestWriteable();
            _Writer.Write(boolean);
        }

        public void Write(byte[] b)
        {
            TestWriteable();
            _Writer.Write(b);
        }

        public void Write(int int32)
        {
            TestWriteable();
            _Writer.Write(int32);
        }

        public void Write(string str)
        {
            TestWriteable();
            _Writer.Write(str);
        }

        public void Write(short int16)
        {
            TestWriteable();
            _Writer.Write(int16);
        }

        public void Write(long int64)
        {
            TestWriteable();
            _Writer.Write(int64);
        }
		
		public void Write(uint uint32){
			TestWriteable();
			_Writer.Write(uint32);
		}
		
		public void Write(ulong uint64){
			TestWriteable();
			_Writer.Write(uint64);
		}
		
		public void Write(ushort uint16){
			TestWriteable();
			_Writer.Write(uint16);
		}

        public void Write(float demicalFloat)
        {
            TestWriteable();
            _Writer.Write(demicalFloat);
        }

        public void Write(IPacketSerializable obj)
        {
            TestWriteable();
            Write(obj.GetType().FullName);
            Write(obj.Serialize(this));
        }

        /// <summary>
        /// Flushes all unflushed data, then sends the packet using a specified send type, and channel.
		///	See:
		/// <see cref="SteamGameServerNetworking.SendP2PPacket"/>
		/// <seealso cref="SteamNetworking.SendP2PPacket"/>
        /// </summary>
        /// <param name="sendType">The type of send for Steam to use.</param>
        /// <param name="nChannel">The channel for steam to pass data through, might want to make sure you add this channel to the client channel list.</param>
        /// <param name="recipients">Who to send the packet to, leave empty for everyone on the server on the send call.</param>
        public bool Send(EP2PSend sendType, int nChannel, params CSteamID[] recipients)
        {
            Flush();

            byte[] bytes = Bytes;

            bool sent = false;

            if(bytes.Length <= 0)
            {
                throw new InvalidOperationException("Tried to send a packet with no data.");
            }

            if (_Server != null)
            {
                if (recipients.Length > 0)
                {
                    foreach (CSteamID player in recipients)
                    {
                        sent = SteamGameServerNetworking.SendP2PPacket(player, bytes, (uint)bytes.Length, sendType, nChannel);
                    }
                }
                else
                {
                    foreach (CSteamID player in _Server.Players.Keys)
                    {
                        sent = SteamGameServerNetworking.SendP2PPacket(player, bytes, (uint)bytes.Length, sendType, nChannel);
                    }
                }
            }
            else
            {
                sent = SteamNetworking.SendP2PPacket(_Client.ConnectedTo, bytes, (uint)bytes.Length, sendType, nChannel);
            }

            // Dispose unnessary objects.
            Dispose();
            return sent;
        }

        public void Flush()
        {
            TestWriteable();
            _Writer.Flush();
        }

        #endregion
		
		public void Seek(int offset = 0)
        {
            _Stream.Seek(offset, SeekOrigin.Begin);
        }

        #region Read Methods

        public Vector3 ReadVector3()
        {
            TestReadable();
            Vector3 vector3 = new Vector3();
            vector3.x = ReadFloat();
            vector3.y = ReadFloat();
            vector3.z = ReadFloat();
            return vector3;
        }

        public Quaternion ReadQuaternion()
        {
            TestReadable();
            Quaternion rotation = new Quaternion();
            rotation.w = ReadFloat();
            rotation.x = ReadFloat();
            rotation.y = ReadFloat();
            rotation.z = ReadFloat();
            return rotation;
        }

        public float ReadFloat()
        {
            TestReadable();
            return _Reader.ReadSingle();
        }

        public byte ReadByte()
        {
            TestReadable();
            return _Reader.ReadByte();
        }

        public char ReadChar()
        {
            TestReadable();
            return _Reader.ReadChar();
        }

        public char[] ReadCharArray(int count)
        {
            TestReadable();
            return _Reader.ReadChars(count);
        }

        public byte[] ReadByteArray(int count)
        {
            TestReadable();
            return  _Reader.ReadBytes(count);
        }
		
		public uint ReadUInteger(){
			TestReadable();
			return _Reader.ReadUInt32();
		}
		
		public ulong ReadULong(){
			TestReadable();
			return _Reader.ReadUInt64();
		}
		
		public ushort ReadUShort(){
			TestReadable();
			return _Reader.ReadUInt16();
		}

        public string ReadString()
        {
            TestReadable();
            return _Reader.ReadString();
        }

        public int ReadInteger()
        {
            TestReadable();
            return _Reader.ReadInt32();
        }

        public short ReadShort()
        {
            TestReadable();
            return _Reader.ReadInt16();
        }

        public long ReadLong()
        {
            TestReadable();
            return _Reader.ReadInt64();
        }

        public decimal ReadDecimal()
        {
            TestReadable();
            return _Reader.ReadDecimal();
        }

        public bool ReadBoolean()
        {
            return _Reader.ReadBoolean();
        }

        public IPacketSerializable ReadSerializableObject()
        {
            TestReadable();
            string type = _Reader.ReadString();
            Assembly asm = Assembly.GetEntryAssembly();
            Type t = asm.GetType(type, false);
            if (t.IsClass)
                return (IPacketSerializable)Activator.CreateInstance(t);
            return default(IPacketSerializable);
        }

        #endregion

        public void Dispose()
        {
            _Stream.Dispose();
            Disposed = true;
        }

    }

    public interface IPacketSerializable
    {

        byte[] Serialize(Packet packet);

        IPacketSerializable Unserialize(Packet packet);

    }

}