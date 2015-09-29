using ServiceStack.Redis;
using Akka.Actor;
using System;
using System.Threading;
using System.Diagnostics;

namespace AkkaNotifications
{
    class Program
    {
        const string ChannelName = "Funky";
        const string MessagePrefix = "MESSAGE ";
        const int NumberOfEvents = 10;
        private static ActorSystem MyActorSystem;
        
        static void Main(string[] args)
        {
            IRedisSubscription sub;
            MyActorSystem = ActorSystem.Create("MyActorSystem");
            var redisPool = new BasicRedisClientManager("127.0.0.1:6379");
            var redisConsumer = redisPool.GetClient();
            var numberOfEventsHeard = 0;

            using (sub = redisConsumer.CreateSubscription())
            { 
                sub.OnMessage = (channel, msg) =>
                {
                    //tell an actor that somethings come in
                    var redisSubActor = MyActorSystem.ActorOf(Props.Create(() => new MyCoordinator(new Messages { Channel = channel, Message = msg })));
                    numberOfEventsHeard++;
                    if (numberOfEventsHeard >= NumberOfEvents)
                        sub.UnSubscribeFromAllChannels();                      

                };
                redisConsumer.PublishMessage(ChannelName, "Hello Adam");

            }

            ThreadPool.QueueUserWorkItem(x =>
            {
                Console.WriteLine("Begin publishing messages...");

                using (var redisPublisher = redisPool.GetClient())
                {
                    for (var i = 1; i <= NumberOfEvents; i++)
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
        public MessageComplete(TimeSpan timeTaken)
        {
            TimeTaken = timeTaken;
        }

        public TimeSpan TimeTaken { get; set; }
    }

    public class MyCoordinator : ReceiveActor
    {
        private IActorRef _childMessageActor;
        private Messages _message;

        public MyCoordinator(Messages message)
        {
            _message = message;
            Console.WriteLine("Queuing {0}", message.Message);
            Receive<MessageComplete>(myMessage =>
            {
                ProcessCompletedMessage(myMessage);
            });
        }
        
        protected override void PreStart()
        {
            _childMessageActor = Context.ActorOf(Props.Create(() => new MyChildActor()));
            _childMessageActor.Tell(new Messages { Channel = _message.Channel, Message = _message.Message });
        }

        private void ProcessCompletedMessage(MessageComplete time)
        {
            _childMessageActor = null;
            Console.WriteLine("Finished work on {0} at channel {1} in {2} ms", _message.Message, _message.Channel, time.TimeTaken.TotalMilliseconds);
        }
    }

    public class MyChildActor : ReceiveActor
    {
        private Stopwatch _totalTime;

        public MyChildActor()
        {
            _totalTime = Stopwatch.StartNew();
            Receive<Messages>(message =>
            {
                ProcessMessage(message.Message, message.Channel);
            });
        }

        private void ProcessMessage(string message, string channel)
        {
            Console.WriteLine("Starting work on {0} at channel {1}", message, channel);
            Thread.Sleep(5000);
            _totalTime.Stop();
            Context.Parent.Tell(new MessageComplete(_totalTime.Elapsed));
        }
    }
}
