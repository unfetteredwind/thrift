// Licensed to the Apache Software Foundation(ASF) under one
// or more contributor license agreements.See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied. See the License for the
// specific language governing permissions and limitations
// under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Thrift;
using Thrift.Collections;
using Thrift.Processor;
using Thrift.Protocol;
using Thrift.Server;
using Thrift.Transport;
using Thrift.Transport.Server;

#pragma warning disable IDE0063  // using can be simplified, we don't

namespace ThriftTest
{
    internal enum ProtocolChoice
    {
        Binary,
        Compact,
        Json
    }

    internal enum TransportChoice
    {
        Socket,
        TlsSocket,
        NamedPipe
    }

    internal enum BufferChoice
    {
        None,
        Buffered,
        Framed
    }

    internal class ServerParam
    {
        internal BufferChoice buffering = BufferChoice.None;
        internal ProtocolChoice protocol = ProtocolChoice.Binary;
        internal TransportChoice transport = TransportChoice.Socket;
        internal int port = 9090;
        internal string pipe = null;

        internal void Parse(List<string> args)
        {
            for (var i = 0; i < args.Count; i++)
            {
                if (args[i].StartsWith("--pipe="))
                {
                    pipe = args[i].Substring(args[i].IndexOf("=") + 1);
                    transport = TransportChoice.NamedPipe;
                }
                else if (args[i].StartsWith("--port="))
                {
                    port = int.Parse(args[i].Substring(args[i].IndexOf("=") + 1));
                    if(transport != TransportChoice.TlsSocket)
                        transport = TransportChoice.Socket;
                }
                else if (args[i] == "-b" || args[i] == "--buffered" || args[i] == "--transport=buffered")
                {
                    buffering = BufferChoice.Buffered;
                }
                else if (args[i] == "-f" || args[i] == "--framed" || args[i] == "--transport=framed")
                {
                    buffering = BufferChoice.Framed;
                }
                else if (args[i] == "--binary" || args[i] == "--protocol=binary")
                {
                    protocol = ProtocolChoice.Binary;
                }
                else if (args[i] == "--compact" || args[i] == "--protocol=compact")
                {
                    protocol = ProtocolChoice.Compact;
                }
                else if (args[i] == "--json" || args[i] == "--protocol=json")
                {
                    protocol = ProtocolChoice.Json;
                }
                else if (args[i] == "--threaded" || args[i] == "--server-type=threaded")
                {
                    throw new NotImplementedException(args[i]);
                }
                else if (args[i] == "--threadpool" || args[i] == "--server-type=threadpool")
                {
                    throw new NotImplementedException(args[i]);
                }
                else if (args[i] == "--prototype" || args[i] == "--processor=prototype")
                {
                    throw new NotImplementedException(args[i]);
                }
                else if (args[i] == "--ssl")
                {
                    transport = TransportChoice.TlsSocket;
                }
                else if (args[i] == "--help")
                {
                    PrintOptionsHelp();
                    return;
                }
                else
                {
                    Console.WriteLine("Invalid argument: {0}", args[i]);
                    PrintOptionsHelp();
                    return;
                }
            }

        }

        internal static void PrintOptionsHelp()
        {
            Console.WriteLine("Server options:");
            Console.WriteLine("  --pipe=<pipe name>");
            Console.WriteLine("  --port=<port number>");
            Console.WriteLine("  --transport=<transport name>    one of buffered,framed  (defaults to none)");
            Console.WriteLine("  --protocol=<protocol name>      one of compact,json  (defaults to binary)");
            Console.WriteLine("  --server-type=<type>            one of threaded,threadpool  (defaults to simple)");
            Console.WriteLine("  --processor=<prototype>");
            Console.WriteLine("  --ssl");
            Console.WriteLine();
        }
    }

    public class TestServer
    {
        #pragma warning disable CA2211
        public static int _clientID = -1;  // use with Interlocked only!
        #pragma warning restore CA2211

        private static readonly TConfiguration Configuration = null;  // or new TConfiguration() if needed

        public delegate void TestLogDelegate(string msg, params object[] values);

        public class MyServerEventHandler : TServerEventHandler
        {
            public int callCount = 0;

            public Task PreServeAsync(CancellationToken cancellationToken)
            {
                callCount++;
                return Task.CompletedTask;
            }

            public Task<object> CreateContextAsync(TProtocol input, TProtocol output, CancellationToken cancellationToken)
            {
                callCount++;
                return Task.FromResult<object>(null);
            }

            public Task DeleteContextAsync(object serverContext, TProtocol input, TProtocol output, CancellationToken cancellationToken)
            {
                callCount++;
                return Task.CompletedTask;
            }

            public Task ProcessContextAsync(object serverContext, TTransport transport, CancellationToken cancellationToken)
            {
                callCount++;
                return Task.CompletedTask;
            }
        }

        public class TestHandlerAsync : ThriftTest.IAsync
        {
            public TServer Server { get; set; }
            private readonly int handlerID;
            private readonly StringBuilder sb = new StringBuilder();
            private readonly TestLogDelegate logger;

            public TestHandlerAsync()
            {
                handlerID = Interlocked.Increment(ref _clientID);
                logger += TestConsoleLogger;
                logger.Invoke("New TestHandler instance created");
            }

            public void TestConsoleLogger(string msg, params object[] values)
            {
                sb.Clear();
                sb.AppendFormat("handler{0:D3}:", handlerID);
                sb.AppendFormat(msg, values);
                sb.AppendLine();
                Console.Write(sb.ToString());
            }

            public Task testVoidAsync(CancellationToken cancellationToken)
            {
                logger.Invoke("testVoid()");
                return Task.CompletedTask;
            }

            public Task<string> testStringAsync(string thing, CancellationToken cancellationToken)
            {
                logger.Invoke("testString({0})", thing);
                return Task.FromResult(thing);
            }

            public Task<bool> testBoolAsync(bool thing, CancellationToken cancellationToken)
            {
                logger.Invoke("testBool({0})", thing);
                return Task.FromResult(thing);
            }

            public Task<sbyte> testByteAsync(sbyte thing, CancellationToken cancellationToken)
            {
                logger.Invoke("testByte({0})", thing);
                return Task.FromResult(thing);
            }

            public Task<int> testI32Async(int thing, CancellationToken cancellationToken)
            {
                logger.Invoke("testI32({0})", thing);
                return Task.FromResult(thing);
            }

            public Task<long> testI64Async(long thing, CancellationToken cancellationToken)
            {
                logger.Invoke("testI64({0})", thing);
                return Task.FromResult(thing);
            }

            public Task<double> testDoubleAsync(double thing, CancellationToken cancellationToken)
            {
                logger.Invoke("testDouble({0})", thing);
                return Task.FromResult(thing);
            }

            public Task<byte[]> testBinaryAsync(byte[] thing, CancellationToken cancellationToken)
            {
                logger.Invoke("testBinary({0} bytes)", thing.Length);
                return Task.FromResult(thing);
            }

            public Task<Xtruct> testStructAsync(Xtruct thing, CancellationToken cancellationToken)
            {
                logger.Invoke("testStruct({{\"{0}\", {1}, {2}, {3}}})", thing.String_thing, thing.Byte_thing, thing.I32_thing, thing.I64_thing);
                return Task.FromResult(thing);
            }

            public Task<Xtruct2> testNestAsync(Xtruct2 nest, CancellationToken cancellationToken)
            {
                var thing = nest.Struct_thing;
                logger.Invoke("testNest({{{0}, {{\"{1}\", {2}, {3}, {4}, {5}}}}})",
                    nest.Byte_thing,
                    thing.String_thing,
                    thing.Byte_thing,
                    thing.I32_thing,
                    thing.I64_thing,
                    nest.I32_thing);
                return Task.FromResult(nest);
            }

            public Task<Dictionary<int, int>> testMapAsync(Dictionary<int, int> thing, CancellationToken cancellationToken)
            {
                sb.Clear();
                sb.Append("testMap({{");
                var first = true;
                foreach (var key in thing.Keys)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        sb.Append(", ");
                    }
                    sb.AppendFormat("{0} => {1}", key, thing[key]);
                }
                sb.Append("}})");
                logger.Invoke(sb.ToString());
                return Task.FromResult(thing);
            }

            public Task<Dictionary<string, string>> testStringMapAsync(Dictionary<string, string> thing, CancellationToken cancellationToken)
            {
                sb.Clear();
                sb.Append("testStringMap({{");
                var first = true;
                foreach (var key in thing.Keys)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        sb.Append(", ");
                    }
                    sb.AppendFormat("{0} => {1}", key, thing[key]);
                }
                sb.Append("}})");
                logger.Invoke(sb.ToString());
                return Task.FromResult(thing);
            }

            public Task<THashSet<int>> testSetAsync(THashSet<int> thing, CancellationToken cancellationToken)
            {
                sb.Clear();
                sb.Append("testSet({{");
                var first = true;
                foreach (int elem in thing)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        sb.Append(", ");
                    }
                    sb.AppendFormat("{0}", elem);
                }
                sb.Append("}})");
                logger.Invoke(sb.ToString());
                return Task.FromResult(thing);
            }

            public Task<List<int>> testListAsync(List<int> thing, CancellationToken cancellationToken)
            {
                sb.Clear();
                sb.Append("testList({{");
                var first = true;
                foreach (var elem in thing)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        sb.Append(", ");
                    }
                    sb.AppendFormat("{0}", elem);
                }
                sb.Append("}})");
                logger.Invoke(sb.ToString());
                return Task.FromResult(thing);
            }

            public Task<Numberz> testEnumAsync(Numberz thing, CancellationToken cancellationToken)
            {
                logger.Invoke("testEnum({0})", thing);
                return Task.FromResult(thing);
            }

            public Task<long> testTypedefAsync(long thing, CancellationToken cancellationToken)
            {
                logger.Invoke("testTypedef({0})", thing);
                return Task.FromResult(thing);
            }

            public Task<Dictionary<int, Dictionary<int, int>>> testMapMapAsync(int hello, CancellationToken cancellationToken)
            {
                logger.Invoke("testMapMap({0})", hello);
                var mapmap = new Dictionary<int, Dictionary<int, int>>();

                var pos = new Dictionary<int, int>();
                var neg = new Dictionary<int, int>();
                for (var i = 1; i < 5; i++)
                {
                    pos[i] = i;
                    neg[-i] = -i;
                }

                mapmap[4] = pos;
                mapmap[-4] = neg;

                return Task.FromResult(mapmap);
            }

            public Task<Dictionary<long, Dictionary<Numberz, Insanity>>> testInsanityAsync(Insanity argument, CancellationToken cancellationToken)
            {
                logger.Invoke("testInsanity()");

                /** from ThriftTest.thrift:
                 * So you think you've got this all worked, out eh?
                 *
                 * Creates a the returned map with these values and prints it out:
                 *   { 1 => { 2 => argument,
                 *            3 => argument,
                 *          },
                 *     2 => { 6 => <empty Insanity struct>, },
                 *   }
                 * @return map<UserId, map<Numberz,Insanity>> - a map with the above values
                 */

                var first_map = new Dictionary<Numberz, Insanity>();
                var second_map = new Dictionary<Numberz, Insanity>(); ;

                first_map[Numberz.TWO] = argument;
                first_map[Numberz.THREE] = argument;

                second_map[Numberz.SIX] = new Insanity();

                var insane = new Dictionary<long, Dictionary<Numberz, Insanity>>
                {
                    [1] = first_map,
                    [2] = second_map
                };

                return Task.FromResult(insane);
            }

            public Task<Xtruct> testMultiAsync(sbyte arg0, int arg1, long arg2, Dictionary<short, string> arg3, Numberz arg4, long arg5,
                CancellationToken cancellationToken)
            {
                logger.Invoke("testMulti()");

                var hello = new Xtruct(); ;
                hello.String_thing = "Hello2";
                hello.Byte_thing = arg0;
                hello.I32_thing = arg1;
                hello.I64_thing = arg2;
                return Task.FromResult(hello);
            }

            public Task testExceptionAsync(string arg, CancellationToken cancellationToken)
            {
                logger.Invoke("testException({0})", arg);
                if (arg == "Xception")
                {
                    var x = new Xception
                    {
                        ErrorCode = 1001,
                        Message = arg
                    };
                    throw x;
                }
                if (arg == "TException")
                {
                    throw new TException();
                }
                return Task.CompletedTask;
            }

            public Task<Xtruct> testMultiExceptionAsync(string arg0, string arg1, CancellationToken cancellationToken)
            {
                logger.Invoke("testMultiException({0}, {1})", arg0, arg1);
                if (arg0 == "Xception")
                {
                    var x = new Xception
                    {
                        ErrorCode = 1001,
                        Message = "This is an Xception"
                    };
                    throw x;
                }

                if (arg0 == "Xception2")
                {
                    var x = new Xception2
                    {
                        ErrorCode = 2002,
                        Struct_thing = new Xtruct { String_thing = "This is an Xception2" }
                    };
                    throw x;
                }

                var result = new Xtruct { String_thing = arg1 };
                return Task.FromResult(result);
            }

            public Task testOnewayAsync(int secondsToSleep, CancellationToken cancellationToken)
            {
                logger.Invoke("testOneway({0}), sleeping...", secondsToSleep);
                Task.Delay(secondsToSleep * 1000, cancellationToken).GetAwaiter().GetResult();
                logger.Invoke("testOneway finished");

                return Task.CompletedTask;
            }
        }


        private static X509Certificate2 GetServerCert()
        {
            var serverCertName = "server.p12";
            var possiblePaths = new List<string>
            {
                "../../../keys/",
                "../../keys/",
                "../keys/",
                "keys/",
            };
                        
            string existingPath = null;
            foreach (var possiblePath in possiblePaths)
            {
                var path = Path.GetFullPath(possiblePath + serverCertName);
                if (File.Exists(path))
                {
                    existingPath = path;
                    break;
                }
            }
                        
            if (string.IsNullOrEmpty(existingPath))
            {
                throw new FileNotFoundException($"Cannot find file: {serverCertName}");
            }
                                    
            var cert = new X509Certificate2(existingPath, "thrift");
                        
            return cert;
        }

        public static int Execute(List<string> args)
        {
            using (var loggerFactory = new LoggerFactory()) //.AddConsole().AddDebug();
            {
                var logger = loggerFactory.CreateLogger("Test");

                try
                {
                    var param = new ServerParam();

                    try
                    {
                        param.Parse(args);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("*** FAILED ***");
                        Console.WriteLine("Error while  parsing arguments");
                        Console.WriteLine(ex.Message + " ST: " + ex.StackTrace);
                        return 1;
                    }


                    // Endpoint transport (mandatory)
                    TServerTransport trans;
                    switch (param.transport)
                    {
                        case TransportChoice.NamedPipe:
                            Debug.Assert(param.pipe != null);
                            trans = new TNamedPipeServerTransport(param.pipe, Configuration, NamedPipeClientFlags.OnlyLocalClients);
                            break;


                        case TransportChoice.TlsSocket:
                            var cert = GetServerCert();
                            if (cert == null || !cert.HasPrivateKey)
                            {
                                cert?.Dispose();
                                throw new InvalidOperationException("Certificate doesn't contain private key");
                            }

                            trans = new TTlsServerSocketTransport(param.port, Configuration,
                                cert,
                                (sender, certificate, chain, errors) => true,
                                null, SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12);
                            break;

                        case TransportChoice.Socket:
                        default:
                            trans = new TServerSocketTransport(param.port, Configuration);
                            break;
                    }

                    // Layered transport (mandatory)
                    TTransportFactory transFactory = null;
                    switch (param.buffering)
                    {
                        case BufferChoice.Framed:
                            transFactory = new TFramedTransport.Factory();
                            break;
                        case BufferChoice.Buffered:
                            transFactory = new TBufferedTransport.Factory();
                            break;
                        default:
                            Debug.Assert(param.buffering == BufferChoice.None, "unhandled case");
                            transFactory = null;  // no layered transprt
                            break;
                    }

                    TProtocolFactory proto = param.protocol switch
                    {
                        ProtocolChoice.Compact => new TCompactProtocol.Factory(),
                        ProtocolChoice.Json => new TJsonProtocol.Factory(),
                        ProtocolChoice.Binary => new TBinaryProtocol.Factory(),
                        _ => new TBinaryProtocol.Factory(),
                    };

                    // Processor
                    var testHandler = new TestHandlerAsync();
                    var testProcessor = new ThriftTest.AsyncProcessor(testHandler);
                    var processorFactory = new TSingletonProcessorFactory(testProcessor);

                    TServer serverEngine = new TSimpleAsyncServer(processorFactory, trans, transFactory, transFactory, proto, proto, logger);

                    //Server event handler
                    var serverEvents = new MyServerEventHandler();
                    serverEngine.SetEventHandler(serverEvents);

                    // Run it
                    var where = (!string.IsNullOrEmpty(param.pipe)) ? "on pipe " + param.pipe : "on port " + param.port;
                    Console.WriteLine("Starting the AsyncBaseServer " + where +
                                      " with processor TPrototypeProcessorFactory prototype factory " +
                                      (param.buffering == BufferChoice.Buffered ? " with buffered transport" : "") +
                                      (param.buffering == BufferChoice.Framed ? " with framed transport" : "") +
                                      (param.transport == TransportChoice.TlsSocket ? " with encryption" : "") +
                                      (param.protocol == ProtocolChoice.Compact ? " with compact protocol" : "") +
                                      (param.protocol == ProtocolChoice.Json ? " with json protocol" : "") +
                                      "...");
                    serverEngine.ServeAsync(CancellationToken.None).GetAwaiter().GetResult();
                    Console.ReadLine();
                }
                catch (Exception x)
                {
                    Console.Error.Write(x);
                    return 1;
                }

                Console.WriteLine("done.");
                return 0;
            }
        }
    }

}
