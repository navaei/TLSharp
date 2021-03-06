﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using Ionic.Crc;
using TLSharp.Core.Auth;
using TLSharp.Core.MTProto;
using TLSharp.Core.MTProto.Crypto;
using TLSharp.Core.Network;
using TLSharp.Core.Utils;
using static TLSharp.Core.MTProto.Serializers;
using TeleSharp.TL;
using TeleSharp.TL.Auth;
using TLSharp.Core.Requests;
using TeleSharp.TL.Contacts;

namespace TlgListenerApplication
{
    class Program
    {
        private static int sequence = 0;
        private static int timeOffset;
        private static long lastMessageId;
        private static Random random => new Random();

        static void Main(string[] args)
        {
            TLContext.Init();
            ObjectUtils.ServerSide = true;
            Console.WriteLine("Listening...");
            TcpListener();
            Console.WriteLine("The end");
            Console.ReadKey();
        }

        private static void TcpListener()
        {
            try
            {
                var tcpListener = new TcpListener(IPAddress.Parse("127.0.0.1"), 5000);
                tcpListener.Start();
                for (; ; )
                {
                    if (tcpListener.Pending())
                    {
                        try
                        {
                            ProcessRequest(tcpListener);
                        }
                        catch (Exception e)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(e);
                            Console.ForegroundColor = ConsoleColor.Gray;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }

        private static void ProcessRequest(TcpListener tcpListener)
        {
            Console.WriteLine("Processing...");
            var tcpClient = tcpListener.AcceptTcpClient();
            var netStream = tcpClient.GetStream();
            BigInteger ga = null;
            byte[] newNonce = new byte[32];
            BigInteger a = new BigInteger(2048, new Random());
            var dhPrime = new BigInteger("00C150023E2F70DB7985DED064759CFECF0AF328E69A41DAF4D6F01B538135A6F91F8F8B2A0EC9BA9720CE352EFCF6C5680FFC424BD634864902DE0B4BD6D49F4E580230E3AE97D95C8B19442B3C0A10D8F5633FECEDD6926A7F6DAB0DDB7D457F9EA81B8465FCD6FFFEED114011DF91C059CAEDAF97625F6C96ECC74725556934EF781D866B34F011FCE4D835A090196E9A5F0E4449AF7EB697DDB9076494CA5F81104A305B6DD27665722C46B60E5DF680FB16B210607EF217652E60236C255F6A28315F4083A96791D7214BF64C1DF4FD0DB1944FB26A2A57031B32EEE64AD15A8BA68885CDE74A5BFC920F6ABF59BA5C75506373E7130F9042DA922179251F", 16);
            BigInteger gb = null;
            var sequenceNumber = 1;
            ulong? messageId = null;
            var privateKey = new BigInteger("582A4D5EE3A45C1AEEBDECD549D1FD4E12337B05C4C0A03FA8FF4A0A7B2861BAB86E8B58A70AAB9CF173FA313348239E28B17D34C7CEC8B68544BAD8623A306D747B7DC1D3D064FA73CE96893E8AFC36F7CDF58A383F48BDEC284D30BFFBC3F1A413DC869B3692EDD26004EE661C021BDA32F124D6631C67891E3E35EEDEAA08BFED8DBB7A6CC1D550CF16C67703BBDFFF0500FD81A55F98D92ECD67CE3CC31B766EA0DFBA284E18677E46036D9ED04105AAD11E97FD675F49A3B54D5AD395AA3C5B8343CDFF70C2E2A9243A47FBC5F541BBAE910B5DD1BF574B1E732A105C2B8F5239A4DFA0BCE0559F18BA0C44D31A279FA7CDCA612BD8F9796EBD114F7FA9", 16);
            AuthKey authKey = null;

            //var getingCounter = 0;
            //while (true)
            //{
            //    if (!netStream.DataAvailable)
            //        continue;
            //    Console.WriteLine("Get data " + ++getingCounter);
            //}

            while (tcpClient.Connected)
            {
                System.Threading.Thread.Sleep(100);
                if (!netStream.DataAvailable) continue;

                byte[] nonceFromClient = new byte[16];
                byte[] servernonce = new byte[16];

                uint responseCode = 0;
                int innerCode = 0;
                long authkeysample = 123456789;

                const long step1Constructor = 0x60469778;
                const long step2Constructor = 0xd712e4be;
                const long step3Constructor = 0xf5045f1f;

                if (netStream.CanRead)
                {
                    var bytes = new byte[tcpClient.ReceiveBufferSize];
                    netStream.Read(bytes, 0, (int)tcpClient.ReceiveBufferSize);
                    var tcpMessage = TcpMessage.Decode(bytes);
                    var binaryReader = new BinaryReader(new MemoryStream(tcpMessage.Body, false));


                    var authKeyId = binaryReader.ReadInt64();
                    if (authKeyId == 0)
                    {
                        var msgId = binaryReader.ReadInt64();
                        var datalength = binaryReader.ReadInt32();
                        var data = binaryReader.ReadBytes(datalength);

                        var binaryReader2 = new BinaryReader(new MemoryStream(data, false));

                        responseCode = binaryReader2.ReadUInt32();
                        Console.WriteLine("Request code: " + responseCode);
                        if (responseCode == step1Constructor) //---Step1_PQRequest
                        {
                            nonceFromClient = binaryReader2.ReadBytes(16);
                        }
                        else if (responseCode == step2Constructor) //---Step1_PQRequest
                        {
                            nonceFromClient = binaryReader2.ReadBytes(16);
                            servernonce = binaryReader2.ReadBytes(16);
                            var p = binaryReader2.ReadBytes(4);
                            var q = binaryReader2.ReadBytes(8);
                            var targetFingerprint = BitConverter.ToString(binaryReader2.ReadBytes(8)).Replace("-", string.Empty);

                            //TODO: need to decryption
                            var ciphertext = Bytes.read(binaryReader2);
                            ciphertext = RSA.Decrypt(targetFingerprint, ciphertext, privateKey, 0, ciphertext.Length);
                            var cipherReader = new BinaryReader(new MemoryStream(ciphertext, false));
                            var hashsum = cipherReader.ReadBytes(20);
                            var innercode = cipherReader.ReadUInt32();//0x83c95aec
                            var pq = cipherReader.ReadBytes(20);
                            var noncetemp = cipherReader.ReadBytes(16);
                            var servernoncetemp = cipherReader.ReadBytes(16);
                            newNonce = cipherReader.ReadBytes(32);
                            //Array.Copy(ciphertext, ciphertext.Length - 32, newNonce, 0, 32);
                            //ciphertext.CopyTo(newnoncetemp, ciphertext.Length - 32);
                        }
                        else if (responseCode == step3Constructor) //---Step1_PQRequest
                        {
                            nonceFromClient = binaryReader2.ReadBytes(16);
                            servernonce = binaryReader2.ReadBytes(16);

                            //TODO: need to decryption
                            var ciphertext = Bytes.read(binaryReader2);
                            AESKeyData key = AES.GenerateKeyDataFromNonces(servernonce, newNonce);
                            var cleartext = AES.DecryptAES(key, ciphertext);
                            var binaryReadernner = new BinaryReader(new MemoryStream(cleartext, false));
                            var hasheddata = binaryReadernner.ReadBytes(20);
                            var client_dh_inner_data_code = binaryReadernner.ReadUInt32();
                            if (client_dh_inner_data_code != 0x6643b654)
                            {
                                throw new Exception("We have a complex story");
                            }
                            var nonceFromClient_temp = binaryReadernner.ReadBytes(16);
                            var servernonce_temp = binaryReadernner.ReadBytes(16);
                            var zero = binaryReadernner.ReadUInt64();
                            gb = new BigInteger(Bytes.read(binaryReadernner));
                        }
                    }
                    else
                    {
                        var _gba = gb.ModPow(a, dhPrime);
                        authKey = new AuthKey(_gba);
                        var decodeMessage = DecodeMessage(tcpMessage.Body, authKey);
                        var objrawReader = new BinaryReader(new MemoryStream(decodeMessage.Item1, false));
                        messageId = decodeMessage.Item2;
                        innerCode = objrawReader.ReadInt32();


                        if (innerCode == 0x62d6b459)//acknowledged 
                        {
                            var vector = objrawReader.ReadInt32();
                            var msgCount = objrawReader.ReadInt32();
                            continue;
                        }
                        else //if (responseCode == -627372787)
                        {
                            objrawReader.BaseStream.Position += -4;
                            var obj = ObjectUtils.DeserializeObject(objrawReader);
                            if (obj is TLRequestInvokeWithLayer)
                            {
                                var invokewithlayer = (TLRequestInvokeWithLayer)obj;
                                if (invokewithlayer.Query is TLRequestInitConnection)
                                {
                                    var requestInitConnection = (TLRequestInitConnection)invokewithlayer.Query;
                                }
                                else if (invokewithlayer.Query is TLRequestSendCode)
                                {
                                    var requestSendCode = (TLRequestSendCode)invokewithlayer.Query;
                                }
                            }
                            else if (obj is TLRequestSendCode)
                            {
                                var requestSendCode = (TLRequestSendCode)obj;
                            }
                            else if (obj is TLRequestSignIn)
                            {
                                var requestSignIn = (TLRequestSignIn)obj;
                            }
                            else if (obj is TLRequestGetContacts)
                            {
                                var requestGetContacts = (TLRequestGetContacts)obj;
                            }
                        }

                        //var keyData = Helpers.CalcKey(buffer, messageKey, false);
                        //var data = AES.DecryptAES(keyData, buffer);
                    }
                }

                if (netStream.CanWrite)
                {

                    var fingerprint = StringToByteArray("216be86c022bb4c3");

                    byte[] outputdata = null;
                    if (responseCode == step1Constructor)
                    {
                        var nonce = new byte[16];
                        new Random().NextBytes(nonce);
                        outputdata = new Step1_Response()
                        {
                            Pq = new BigInteger(1, BitConverter.GetBytes(880)),
                            ServerNonce = nonceFromClient,
                            Nonce = nonce,
                            Fingerprints = new List<byte[]>() { fingerprint }
                        }.ToBytes();
                    }
                    else if (responseCode == step2Constructor)
                    {
                        //var nonce = new byte[16];
                        //new Random().NextBytes(nonce);

                        byte[] answer;
                        var hashsum = Encoding.UTF8.GetBytes("asdfghjklmnbvcxzasdf");
                        const uint innerCodetemp = 0xb5890dba;
                        AESKeyData key = AES.GenerateKeyDataFromNonces(servernonce, newNonce);

                        var g = 47;
                        ga = BigInteger.ValueOf(g).ModPow(a, dhPrime);

                        using (var memoryStream = new MemoryStream())
                        {
                            using (var binaryWriter = new BinaryWriter(memoryStream))
                            {
                                binaryWriter.Write(hashsum);
                                binaryWriter.Write(innerCodetemp);
                                binaryWriter.Write(nonceFromClient);
                                binaryWriter.Write(servernonce);
                                binaryWriter.Write(g);
                                Bytes.write(binaryWriter, dhPrime.ToByteArrayUnsigned());
                                Bytes.write(binaryWriter, ga.ToByteArrayUnsigned());
                                Bytes.write(binaryWriter, BitConverter.GetBytes((int)(Convert.ToInt64((DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds) / 1000)));//server datetime
                                answer = memoryStream.ToArray();
                            }
                        }

                        outputdata = new Step2_Response()
                        {
                            ServerNonce = nonceFromClient,
                            Nonce = servernonce,
                            NewNonce = newNonce,
                            EncryptedAnswer = AES.EncryptAES(key, answer)
                        }.ToBytes();
                    }
                    else if (responseCode == step3Constructor)
                    {
                        var _gba = gb.ModPow(a, dhPrime);
                        authKey = new AuthKey(_gba);
                        var newNonceHash = authKey.CalcNewNonceHash(newNonce, 1);
                        const uint innerCodeTemp = 0x3bcbf734;
                        using (var memoryStream = new MemoryStream())
                        {
                            using (var binaryWriter = new BinaryWriter(memoryStream))
                            {
                                binaryWriter.Write(innerCodeTemp);
                                binaryWriter.Write(servernonce);
                                binaryWriter.Write(nonceFromClient);
                                binaryWriter.Write(newNonceHash);//hashnewnonce
                                outputdata = memoryStream.ToArray();
                            }
                        }
                    }
                    else if (innerCode == -2035355412)//TLRequestSendCode
                    {
                        #region Generate TLSentCode

                        var sentCode = new TLSentCode();
                        sentCode.PhoneRegistered = false;
                        sentCode.Timeout = 7777;
                        sentCode.PhoneCodeHash = "asdfghjklmnbvcxzasdf";
                        sentCode.Flags = 3;
                        sentCode.NextType = new TLCodeTypeSms();
                        sentCode.Type = new TLSentCodeTypeApp() { Length = 20 };

                        #endregion

                        using (var memoryStream = new MemoryStream())
                        {
                            using (var binaryWriter = new BinaryWriter(memoryStream))
                            {
                                binaryWriter.Write(0xf35c6d01);//main code
                                binaryWriter.Write(messageId.Value);//requestId -- ulong -- from mesage id 
                                sentCode.SerializeBody(binaryWriter);
                                outputdata = memoryStream.ToArray();
                            }
                        }
                    }
                    else if (innerCode == -627372787)
                    {
                        #region Generate TLConfig
                        //---Genrate mock tlconfig
                        var config = new TLConfig();
                        config.CallConnectTimeoutMs = 7777;
                        config.CallPacketTimeoutMs = 7777;
                        config.CallReceiveTimeoutMs = 7777;
                        config.CallRingTimeoutMs = 7777;
                        config.ChatBigSize = 7777;
                        config.ChatSizeMax = 777;
                        config.Date = Convert.ToInt32((DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds);
                        config.DcOptions = new TLVector<TLDcOption>()
                        {
                            new TLDcOption(){Flags=0,Id=1,IpAddress="127.0.0.1",Port=5000 }
                        };
                        config.DisabledFeatures = new TLVector<TLDisabledFeature>();
                        config.ForwardedCountMax = 777;
                        config.MegagroupSizeMax = 777;
                        config.NotifyCloudDelayMs = 7777;
                        config.NotifyDefaultDelayMs = 7777;
                        config.OfflineBlurTimeoutMs = 7777;
                        config.OfflineIdleTimeoutMs = 7777;
                        config.OnlineCloudTimeoutMs = 7777;
                        config.OnlineUpdatePeriodMs = 7777;
                        config.PhonecallsEnabled = false;
                        config.PinnedDialogsCountMax = 7;
                        config.PushChatLimit = 7;
                        config.PushChatPeriodMs = 777;
                        config.RatingEDecay = 777;
                        config.SavedGifsLimit = 777;
                        config.StickersRecentLimit = 777;
                        config.ThisDc = 1;//TODO: ---what's this?!---
                        config.MeUrlPrefix = "https://t.me/";
                        config.TestMode = false;
                        #endregion

                        using (var memoryStream = new MemoryStream())
                        {
                            using (var binaryWriter = new BinaryWriter(memoryStream))
                            {
                                binaryWriter.Write(0xf35c6d01);//main code
                                                               //binaryWriter.Write(0xf35c6d02);//code
                                binaryWriter.Write(messageId.Value);//requestId -- ulong -- from mesage id
                                                                    //binaryWriter.Write(0x2144ca17);//innercode -- int
                                                                    //binaryWriter.Write(1123456789);//sample code
                                                                    //Serializers.Bytes.write(binaryWriter, config.Serialize());
                                config.SerializeBody(binaryWriter);
                                outputdata = memoryStream.ToArray();
                            }
                        }

                    }
                    else if (innerCode == -1126886015)
                    {
                        #region Generate TLAuthorization

                        var auth = new TeleSharp.TL.Auth.TLAuthorization();
                        auth.Flags = 3;
                        auth.User = new TLUser() { FirstName = "Meysami" };

                        #endregion

                        using (var memoryStream = new MemoryStream())
                        {
                            using (var binaryWriter = new BinaryWriter(memoryStream))
                            {
                                binaryWriter.Write(0xf35c6d01);//main code
                                binaryWriter.Write(messageId.Value);//requestId -- ulong -- from mesage id 
                                auth.SerializeBody(binaryWriter);
                                outputdata = memoryStream.ToArray();
                            }
                        }
                    }
                    else if (innerCode == 583445000)//GetContacts
                    {
                        #region Generate TLAbsContacts

                        var contacts = new TLContacts();
                        contacts.Contacts = new TLVector<TLContact>()
                        {
                            new TLContact(){UserId=11 },
                            new TLContact(){UserId=12 }
                        };
                        contacts.Users = new TLVector<TLAbsUser>() {
                            new TLUser(){ Bot=false,FirstName="Mary",Id=11},
                            new TLUser(){ Bot=false,FirstName="Mary 2",Id=12}
                        };

                        #endregion

                        using (var memoryStream = new MemoryStream())
                        {
                            using (var binaryWriter = new BinaryWriter(memoryStream))
                            {
                                binaryWriter.Write(0xf35c6d01);//main code
                                binaryWriter.Write(messageId.Value);//requestId -- ulong -- from mesage id 
                                contacts.SerializeBody(binaryWriter);
                                outputdata = memoryStream.ToArray();
                            }
                        }
                    }
                    else
                    {
                        continue;
                    }

                    if (innerCode != 0)
                    {
                        outputdata = PrepareToSend2(outputdata, authKey.Id, 0, 0, 0, servernonce, sequenceNumber, authKey);
                    }
                    else
                        outputdata = PrepareToSend(outputdata);

                    outputdata = Encode(outputdata, sequenceNumber++);
                    netStream.Write(outputdata, 0, outputdata.Length);
                }
                else
                {
                    Console.WriteLine("You cannot write data to this stream.");
                    tcpClient.Close();
                    netStream.Close();
                }
            }
        }

        private static void ProcessRequestSocket(TcpListener tcpListener)
        {
            Console.WriteLine("Processing...");
            var tcpClient = tcpListener.AcceptSocket();

            var bytes = new byte[tcpClient.ReceiveBufferSize];
            var countbyte = tcpClient.Receive(bytes);

            return;

            byte[] nonceFromClient = new byte[16];
            var tcpMessage = TcpMessage.Decode(bytes);
            var binaryReader = new BinaryReader(new MemoryStream(tcpMessage.Body, false));
            var a = binaryReader.ReadInt64();
            var msgId = binaryReader.ReadInt64();
            var datalength = binaryReader.ReadInt32();
            var data = binaryReader.ReadBytes(datalength);

            var binaryReader2 = new BinaryReader(new MemoryStream(data, false));
            const int responseConstructorNumber = 0x60469778;
            var responseCode = binaryReader2.ReadInt32();
            Console.WriteLine("Request code: " + responseCode);
            if (responseCode == responseConstructorNumber)//---Step1_PQRequest
            {
                nonceFromClient = binaryReader2.ReadBytes(16);
            }

            var nonce = new byte[16];
            new Random().NextBytes(nonce);

            var fingerprint = StringToByteArray("216be86c022bb4c3");
            //var rr = BitConverter.ToString(fingerprint).Replace("-", "");

            var step1 = new Step1_Response()
            {
                Pq = new BigInteger(1, BitConverter.GetBytes(880)),
                ServerNonce = nonceFromClient,
                Nonce = nonce,
                Fingerprints = new List<byte[]>() { fingerprint }
            };
            var bytes1 = PrepareToSend(step1.ToBytes());
            var datatosend = Encode(bytes1, 11);
            //Byte[] sendBytes = Encoding.UTF8.GetBytes("Is anybody there?");
            tcpClient.Send(datatosend, SocketFlags.Truncated);

            //tcpClient.Close();
        }

        private static async Task<TcpMessage> Receieve(TcpClient tcpClient)
        {
            var stream = tcpClient.GetStream();

            var packetLengthBytes = new byte[4];
            if (await stream.ReadAsync(packetLengthBytes, 0, 4) != 4)
                throw new InvalidOperationException("Couldn't read the packet length");
            int packetLength = BitConverter.ToInt32(packetLengthBytes, 0);

            var seqBytes = new byte[4];
            if (await stream.ReadAsync(seqBytes, 0, 4) != 4)
                throw new InvalidOperationException("Couldn't read the sequence");
            int seq = BitConverter.ToInt32(seqBytes, 0);

            int readBytes = 0;
            var body = new byte[packetLength - 12];
            int neededToRead = packetLength - 12;

            do
            {
                var bodyByte = new byte[packetLength - 12];
                var availableBytes = await stream.ReadAsync(bodyByte, 0, neededToRead);
                neededToRead -= availableBytes;
                Buffer.BlockCopy(bodyByte, 0, body, readBytes, availableBytes);
                readBytes += availableBytes;
            }
            while (readBytes != packetLength - 12);

            var crcBytes = new byte[4];
            if (await stream.ReadAsync(crcBytes, 0, 4) != 4)
                throw new InvalidOperationException("Couldn't read the crc");
            int checksum = BitConverter.ToInt32(crcBytes, 0);

            byte[] rv = new byte[packetLengthBytes.Length + seqBytes.Length + body.Length];

            Buffer.BlockCopy(packetLengthBytes, 0, rv, 0, packetLengthBytes.Length);
            Buffer.BlockCopy(seqBytes, 0, rv, packetLengthBytes.Length, seqBytes.Length);
            Buffer.BlockCopy(body, 0, rv, packetLengthBytes.Length + seqBytes.Length, body.Length);
            var crc32 = new Ionic.Crc.CRC32();
            crc32.SlurpBlock(rv, 0, rv.Length);
            var validChecksum = crc32.Crc32Result;

            if (checksum != validChecksum)
            {
                throw new InvalidOperationException("invalid checksum! skip");
            }

            return new TcpMessage(seq, body);
        }

        public static byte[] PrepareToSend(byte[] data)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var binaryWriter = new BinaryWriter(memoryStream))
                {
                    binaryWriter.Write((long)0);
                    binaryWriter.Write(GetNewMessageId());
                    binaryWriter.Write(data.Length);
                    binaryWriter.Write(data);

                    byte[] packet = memoryStream.ToArray();

                    return packet;
                }
            }
        }

        public static byte[] PrepareToSend2(byte[] message, ulong authKeyId, ulong salt, ulong sessionId, ulong messageId, byte[] servernonce, int sequenceNumber, AuthKey authKey)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var binaryWriter = new BinaryWriter(memoryStream))
                {
                    //binaryWriter.Write(servernonce);
                    binaryWriter.Write(salt);//salt
                    binaryWriter.Write(sessionId);//sessionId
                    binaryWriter.Write(messageId);//messageid
                    binaryWriter.Write(sequenceNumber);

                    binaryWriter.Write(message.Length);
                    binaryWriter.Write(message);

                    message = memoryStream.ToArray();
                }
            }

            byte[] msgKey = Helpers.CalcMsgKey(message);
            AESKeyData key = Helpers.CalcKey(authKey.Data, msgKey, false);
            message = AES.EncryptAES(key, message);

            using (var memoryStream = new MemoryStream())
            {
                using (var binaryWriter = new BinaryWriter(memoryStream))
                {
                    binaryWriter.Write(authKeyId);
                    binaryWriter.Write(msgKey);
                    binaryWriter.Write(message);

                    return memoryStream.ToArray();
                }
            }
        }

        private static long GetNewMessageId()
        {
            long time = Convert.ToInt64((DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds);
            long newMessageId = ((time / 1000 + timeOffset) << 32) |
                                ((time % 1000) << 22) |
                                (random.Next(524288) << 2); // 2^19
            // [ unix timestamp : 32 bit] [ milliseconds : 10 bit ] [ buffer space : 1 bit ] [ random : 19 bit ] [ msg_id type : 2 bit ] = [ msg_id : 64 bit ]

            if (lastMessageId >= newMessageId)
            {
                newMessageId = lastMessageId + 4;
            }

            lastMessageId = newMessageId;
            return newMessageId;
        }

        #region helpers
        private static Tuple<byte[], ulong, int> DecodeMessage(byte[] body, AuthKey authkey)
        {
            byte[] message;
            ulong remoteMessageId;
            int remoteSequence;

            using (var inputStream = new MemoryStream(body))
            using (var inputReader = new BinaryReader(inputStream))
            {
                if (inputReader.BaseStream.Length < 8)
                    throw new InvalidOperationException($"Can't decode packet");

                ulong remoteAuthKeyId = inputReader.ReadUInt64(); // TODO: check auth key id
                byte[] msgKey = inputReader.ReadBytes(16); // TODO: check msg_key correctness
                AESKeyData keyData = Helpers.CalcKey(authkey.Data, msgKey, true);
                //TODO: return to decryption
                var cipherText = inputReader.ReadBytes((int)(inputStream.Length - inputStream.Position));
                byte[] plaintext = AES.DecryptAES(keyData, cipherText);
                //byte[] plaintext = inputReader.ReadBytes((int)(inputStream.Length - inputStream.Position));

                using (MemoryStream plaintextStream = new MemoryStream(plaintext))
                using (BinaryReader plaintextReader = new BinaryReader(plaintextStream))
                {
                    var remoteSalt = plaintextReader.ReadUInt64();
                    var remoteSessionId = plaintextReader.ReadUInt64();
                    remoteMessageId = plaintextReader.ReadUInt64();
                    remoteSequence = plaintextReader.ReadInt32();
                    int msgLen = plaintextReader.ReadInt32();
                    message = plaintextReader.ReadBytes(msgLen);
                }
            }
            return new Tuple<byte[], ulong, int>(message, remoteMessageId, remoteSequence);
        }

        private static byte[] Encode(byte[] body, int sequneceNumber)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var binaryWriter = new BinaryWriter(memoryStream))
                {
                    // https://core.telegram.org/mtproto#tcp-transport
                    /*
                        4 length bytes are added at the front 
                        (to include the length, the sequence number, and CRC32; always divisible by 4)
                        and 4 bytes with the packet sequence number within this TCP connection 
                        (the first packet sent is numbered 0, the next one 1, etc.),
                        and 4 CRC32 bytes at the end (length, sequence number, and payload together).
                    */
                    binaryWriter.Write(body.Length + 12);
                    binaryWriter.Write(sequneceNumber);
                    binaryWriter.Write(body);
                    var crc32 = new CRC32();
                    crc32.SlurpBlock(memoryStream.GetBuffer(), 0, 8 + body.Length);
                    binaryWriter.Write(crc32.Crc32Result);

                    var transportPacket = memoryStream.ToArray();

                    //					Debug.WriteLine("Tcp packet #{0}\n{1}", SequneceNumber, BitConverter.ToString(transportPacket));

                    return transportPacket;
                }
            }
        }

        public static T FromByteArray<T>(byte[] data)
        {
            if (data == null)
                return default(T);
            var bf = new BinaryFormatter();
            using (var ms = new MemoryStream(data))
            {
                var obj = bf.Deserialize(ms);
                return (T)obj;
            }
        }

        private static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                .ToArray();
        }

        private static object DeserializeObject(BinaryReader reader)
        {
            int Constructor = reader.ReadInt32();
            object obj;
            Type t = null;
            try
            {
                t = TLContext.getType(Constructor);
                obj = Activator.CreateInstance(t);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Constructor Invalid Or Context.Init Not Called !", ex);
            }
            if (t.IsSubclassOf(typeof(TLMethod)))
            {
                //((TLMethod)obj).DeserializeResponse(reader);
                //return obj;
                ((TLMethod)obj).DeserializeBody(reader);
                return obj;
            }
            else if (t.IsSubclassOf(typeof(TLObject)))
            {
                ((TLObject)obj).DeserializeBody(reader);
                return obj;
            }
            else throw new NotImplementedException("Weird Type : " + t.Namespace + " | " + t.Name);
        }
        #endregion
    }
}
