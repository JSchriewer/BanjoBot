using Discord;
using System;
using System.Collections.Generic;
using System.Threading;

namespace BanjoBotCore.Controller
{
    public class DiscordMessageDispatcher
    {
        public const int MAX_MESSAGE_LENGTH = 2000;
        public const int INTERVAL = 2000;

        private Queue<Message> queue = new Queue<Message>();

        public DiscordMessageDispatcher()
        {
        }

        public void AddQueue(Message message)
        {
            lock (queue)
            {
                queue.Enqueue(message);
            }
        }

        public Message RemoveQueue()
        {
            lock (queue)
            {
                return queue.Dequeue();
            }
        }

        public void Run()
        {
            while (true)
            {
                //Gather messages
                Thread.Sleep(INTERVAL);
                List<Message> messages = new List<Message>();
                for (int i = 0; i < queue.Count; i++)
                {
                    messages.Add(RemoveQueue());
                }

                //Clear message backlog
                while (messages.Count > 0)
                {
                    //Find all messages that will be posted in the same channel
                    List<Message> channelMessages = new List<Message>();
                    Message message = messages[0];
                    IMessageChannel channel = message.channel;

                    foreach (Message channelMessage in messages)
                    {
                        if (channelMessage.channel == channel)
                        {
                            channelMessages.Add(channelMessage);
                        }
                    }

                    //Send messages in chunks to reduce api calls
                    String msg = "";
                    foreach (Message channelMessage in messages)
                    {
                        if (msg.Length + channelMessage.text.Length < 2000)
                        {
                            msg += channelMessage.text + "\n";
                        }
                        else
                        {
                            //TODO: Bug -> channelessage will be ignored
                            channelMessage.channel.SendMessageAsync(msg);
                            msg = channelMessage.text + "\n";
                        }
                    }
                    if (msg.Length > 0)
                        channel.SendMessageAsync(msg);

                    messages.RemoveAll(m => m.channel == channel);
                }
            }
        }

        public struct Message
        {
            public IMessageChannel channel;
            public String text;

            public Message(IMessageChannel channel, String text)
            {
                this.channel = channel;
                this.text = text;
            }
        }
    }
}