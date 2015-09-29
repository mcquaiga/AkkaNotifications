using ServiceStack.Redis;
using Akka.Actor;
using System;

namespace AkkaNotifications
{
    class Program
    {
        const string ChannelName = "Funky";
        const string MessagePrefix = "MESSAGE ";
        private static ActorSystem MyActorSystem;

        static void Main(string[] args)
        {
            IRedisSubscription sub;
            MyActorSystem = ActorSystem.Create("MyActorSystem");

            var redisPool = new BasicRedisClientManager("127.0.0.1:6379");
            var redisConsumer = redisPool.GetClient();
            var redisSubActor = MyActorSystem.ActorOf(Props.Create(() => new MyActor()));

            using (sub = redisConsumer.CreateSubscription())
            { 
                sub.OnMessage = (channel, msg) =>
                {
                    //tell an actor that somethings come in
                    redisSubActor.Tell(new Messages { Channel = channel, Message = msg } );
                };
                redisConsumer.PublishMessage(ChannelName, "Hello Adam");
                sub.SubscribeToChannels(new string[] { ChannelName });
            }

          

            Console.ReadLine();
        }
    }

    public class Messages
    {
        public string Channel { get; set; }
        public string Message { get; set; }
    }

    public class MyActor : UntypedActor
    {
        protected override void OnReceive(object message)
        {
            if (message is Messages)
            {
                var m = (Messages)message;
                Console.WriteLine("Received {0} from channel {1}", m.Message, m.Channel);
            }     
        }
    }
}
