using ServiceStack.Redis;
using Akka.Actor;
using System;
using System.Threading;

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
            
            using (sub = redisConsumer.CreateSubscription())
            { 
                sub.OnMessage = (channel, msg) =>
                {
                    //tell an actor that somethings come in
                    var redisSubActor = MyActorSystem.ActorOf(Props.Create(() => new MyCoordinator(new Messages { Channel = channel, Message = msg })));
                };
                redisConsumer.PublishMessage(ChannelName, "Hello Adam");

            }

            ThreadPool.QueueUserWorkItem(x =>
            {
                Thread.Sleep(200);
                Console.WriteLine("Begin publishing messages...");

                using (var redisPublisher = redisPool.GetClient())
                {
                    for (var i = 1; i <= 1000; i++)
                    {
                        var message = MessagePrefix + i;
                        Console.WriteLine(String.Format("Publishing '{0}' to '{1}'", message, ChannelName));
                        redisPublisher.PublishMessage(ChannelName, message);
                    }
                }
            });

            sub.SubscribeToChannels(new string[] { ChannelName });
            Console.ReadLine();
        }
    }

    public class Messages
    {
        public string Channel { get; set; }
        public string Message { get; set; }
    }

    public class MessageComplete
    {
        public MessageComplete(int timeTaken)
        {
            TimeTaken = timeTaken;
        }

        public int TimeTaken { get; set; }
    }

    public class MyCoordinator : ReceiveActor
    {
        private IActorRef _childMessageActor;
        private Messages _message;

        public MyCoordinator(Messages message)
        {
            _message = message;
            Receive<MessageComplete>(myMessage =>
            {
                ProcessCompletedMessage(myMessage.TimeTaken);
            });
        }
        
        protected override void PreStart()
        {
            _childMessageActor = Context.ActorOf(Props.Create(() => new MyChildActor()));
            _childMessageActor.Tell(new Messages { Channel = _message.Channel, Message = _message.Message });
        }

        private void ProcessCompletedMessage(int time)
        {
            Console.WriteLine(time);
        }
    }

    public class MyChildActor : ReceiveActor
    {
        public MyChildActor()
        {
            Initialize();
        }

        public void Initialize()
        {
            Receive<Messages>(message =>
            {
                ProcessMessage(string.Format("Received '{0}' from channel '{1}'", message.Message, message.Channel));
            });
        }

        private void ProcessMessage(string message)
        {
            Thread.Sleep(100);
            Console.WriteLine(message);
        }
    }
}
