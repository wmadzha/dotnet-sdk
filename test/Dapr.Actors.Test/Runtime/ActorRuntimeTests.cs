﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Test
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Dapr.Actors;
    using Dapr.Actors.Runtime;
    using Xunit;

    public sealed class ActorRuntimeTests
    {
        private const string RenamedActorTypeName = "MyRenamedActor";

        private interface ITestActor : IActor
        {
        }

        [Fact]
        public void TestInferredActorType()
        {
            var actorType = typeof(TestActor);
            var actorRuntime = new ActorRuntime();

            Assert.Empty(actorRuntime.RegisteredActorTypes);

            actorRuntime.RegisterActor<TestActor>();

            Assert.Contains(actorType.Name, actorRuntime.RegisteredActorTypes, StringComparer.InvariantCulture);
        }

        [Fact]
        public void TestExplicitActorType()
        {
            var actorType = typeof(RenamedActor);
            var actorRuntime = new ActorRuntime();

            Assert.NotEqual(RenamedActorTypeName, actorType.Name);

            Assert.Empty(actorRuntime.RegisteredActorTypes);

            actorRuntime.RegisterActor<RenamedActor>();

            Assert.Contains(RenamedActorTypeName, actorRuntime.RegisteredActorTypes, StringComparer.InvariantCulture);
        }

        // This tests the change that removed the Activate message from Dapr runtime -> app.
        [Fact]
        public void NoActivateMessageFromRuntime()
        {
            var actorType = typeof(MyActor);

            ActorRuntime.Instance.RegisterActor<MyActor>();

            var output = new MemoryStream();
            ActorRuntime.DispatchWithoutRemotingAsync("MyActor", "abc", "MyMethod", new MemoryStream(), output).GetAwaiter().GetResult();
            string s = Encoding.UTF8.GetString(output.ToArray());

            Assert.Equal("\"hi\"", s);            
            Assert.Contains(actorType.Name, ActorRuntime.Instance.RegisteredActorTypes, StringComparer.InvariantCulture);
            Console.WriteLine("done");
        }

        [Fact]
        public void TestActorSettings()
        {
            var actorType = typeof(TestActor);
            var actorRuntime = new ActorRuntime();

            Assert.Empty(actorRuntime.RegisteredActorTypes);

            actorRuntime.ConfigureActorSettings(a =>
            {
                a.ActorIdleTimeout = TimeSpan.FromSeconds(33);
                a.ActorScanInterval = TimeSpan.FromSeconds(44);
                a.DrainOngoingCallTimeout = TimeSpan.FromSeconds(55);
                a.DrainRebalancedActors = true;
            });

            actorRuntime.RegisterActor<TestActor>();

            Assert.Contains(actorType.Name, actorRuntime.RegisteredActorTypes, StringComparer.InvariantCulture);

            ArrayBufferWriter<byte> writer = new ArrayBufferWriter<byte>();
            actorRuntime.SerializeSettingsAndRegisteredTypes(writer).GetAwaiter().GetResult();

            // read back the serialized json
            var array = writer.WrittenSpan.ToArray();
            string s = Encoding.UTF8.GetString(array, 0, array.Length);

            JsonDocument document = JsonDocument.Parse(s);
            JsonElement root = document.RootElement;

            // parse out the entities array 
            JsonElement element = root.GetProperty("entities");
            Assert.Equal(1, element.GetArrayLength());

            JsonElement arrayElement = element[0];
            string actor = arrayElement.GetString();
            Assert.Equal("TestActor", actor);

            // validate the other properties have expected values
            element = root.GetProperty("actorIdleTimeout");
            Assert.Equal(TimeSpan.FromSeconds(33), ConverterUtils.ConvertTimeSpanFromDaprFormat(element.GetString()));

            element = root.GetProperty("actorScanInterval");
            Assert.Equal(TimeSpan.FromSeconds(44), ConverterUtils.ConvertTimeSpanFromDaprFormat(element.GetString()));

            element = root.GetProperty("drainOngoingCallTimeout");
            Assert.Equal(TimeSpan.FromSeconds(55), ConverterUtils.ConvertTimeSpanFromDaprFormat(element.GetString()));

            element = root.GetProperty("drainRebalancedActors");
            Assert.True(element.GetBoolean());
        }

        private sealed class TestActor : Actor, ITestActor
        {
            public TestActor(ActorService actorService, ActorId actorId)
                : base(actorService, actorId)
            {
            }
        }

        [Actor(TypeName = RenamedActorTypeName)]
        private sealed class RenamedActor : Actor, ITestActor
        {
            public RenamedActor(ActorService actorService, ActorId actorId)
                : base(actorService, actorId)
            {
            }
        }

        private interface IAnotherActor : IActor
        {
            public Task<string> MyMethod();
        }

        private sealed class MyActor : Actor, IAnotherActor
        {
            public MyActor(ActorService actorService, ActorId actorId)
                : base(actorService, actorId)
            {
            }

            public Task<string> MyMethod()
            {
                return Task.FromResult("hi");
            }
        }
    }
}
