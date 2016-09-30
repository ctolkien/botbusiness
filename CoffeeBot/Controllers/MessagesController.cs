using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using Microsoft.Bot.Builder.FormFlow;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Internals;
using Autofac;
using System.Threading;

namespace CoffeeBot
{

    public enum CoffeeOptions
    {
        Cappuccino,
        Latte,
        FlatWhite,
        LongBlack
    }
    public enum MilkOptions
    {
        FullCream,
        Skim,
        Soy

    }
    public enum SizeOptions
    {
        Regular,
        Large
    }

    public enum SugarOptions
    {
        Zero,
        One,
        Two
    }

    [Serializable]
    public class CoffeeOrder
    {
        public CoffeeOptions? Coffee { get; set; }
        public MilkOptions? Milk { get; set; }
        public SizeOptions? Size { get; set; }
        public SugarOptions? Sugar { get; set; }

        public override string ToString()
        {
            return $"{Coffee} {Milk} {Size} {Sugar}";
        }

        public static IForm<CoffeeOrder> BuildForm()
        {

            OnCompletionAsyncDelegate<CoffeeOrder> processOrder = async (context, state) =>
            {
                await context.PostAsync("We've got your order!");
            };

            return new FormBuilder<CoffeeOrder>()
                .Field(nameof(Coffee))
                .Field(nameof(Milk))
                .Field(nameof(Size))
                .Field(nameof(Sugar))
                //.OnCompletion(processOrder)
                .Build();
        }

    }
    [Serializable]
    public class RootDialog : IDialog<object>
    {
        public string InitiatedBy { get; }

        public RootDialog(string initiatedBy)
        {
            InitiatedBy = initiatedBy;
        }
        public async Task StartAsync(IDialogContext context)
        {
            await context.PostAsync($"{InitiatedBy} is going on a coffee run!");
            context.Wait(MessageReceivedAsync);
        }

        public virtual async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var message = await result;

            await context.Forward(new CoffeeDialog(), this.ResumeAfterCoffeeDialog, message, CancellationToken.None);

        }

        private async Task ResumeAfterCoffeeDialog(IDialogContext context, IAwaitable<CoffeeOrder> result)
        {
            var coffee = await result;
            
            await context.PostAsync($"Thanks! You ordered a: {coffee}");
            context.ConversationData.SetValue("CoffeeOrder", coffee);
            context.Wait(this.MessageReceivedAsync);
        }
    }
    [Serializable]
    public class CoffeeDialog : IDialog<CoffeeOrder>
    {
        
        public async Task StartAsync(IDialogContext context)
        {
            var dialog = FormDialog.FromForm(CoffeeOrder.BuildForm, FormOptions.None);
            context.Call(dialog, ResumeAfter);

        }
        private async Task ResumeAfter(IDialogContext context, IAwaitable<CoffeeOrder> result)
        {
            try
            {
                var coffee = await result;
                context.Done(coffee);

            }
            catch (FormCanceledException<CoffeeOrder> e)
            {
                await context.PostAsync("What? Had enough coffee?!");
                context.Done<object>(null);

            }
            
        }
    }


    public class MessagesController : ApiController
    {

        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        //[BotAuthentication]
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            //var coffeeBotMentioned = activity.Entities
            //    .Where(x => x.Type == "mention")
            //    .Any(x => x.Properties.GetValue("mentioned").Value<string>("name") == "coffeebot");

            var connector = new ConnectorClient(new Uri(activity.ServiceUrl));


            if (activity.Type == ActivityTypes.Message && activity.Text == "done")
            {
                var stateClient = activity.GetStateClient();

                await connector.Conversations.SendToConversationAsync(activity.CreateReply($"ChannelId: {activity.ChannelId}"));
                await connector.Conversations.SendToConversationAsync(activity.CreateReply($"User: {activity.From.Id} {activity.From.Name}"));
                await connector.Conversations.SendToConversationAsync(activity.CreateReply($"Conversation: {activity.Conversation.Id} {activity.Conversation.Name} {activity.Conversation.IsGroup}"));

                var conversationData = await stateClient.BotState.GetConversationDataAsync(activity.ChannelId, activity.Conversation.Id);

                await connector.Conversations.SendToConversationAsync(activity.CreateReply($"{conversationData.Data}"));

                await connector.Conversations.SendToConversationAsync(new Activity(type: ActivityTypes.Message, channelId: activity.ChannelId, conversation: activity.Conversation, text: "woooah nelly"));


            }
            else if (activity.Text == ActivityTypes.ConversationUpdate)
            {
                await connector.Conversations.SendToConversationAsync(activity.CreateReply($"Conversation Updated!"));

                await connector.Conversations.SendToConversationAsync(activity.CreateReply($"Names Added: {activity.MembersAdded.Select(z => z.Name)}"));
                await connector.Conversations.SendToConversationAsync(activity.CreateReply($"Names Removed: {activity.MembersRemoved.Select(z => z.Name)}"));

            }
            else if (activity.Type == ActivityTypes.Message)
            {
                
                await Conversation.SendAsync(activity, () => new RootDialog(activity.From.Name));
            }
            else
            {
                HandleSystemMessage(activity);
            }
            var response = Request.CreateResponse(HttpStatusCode.OK);
            return response;
        }

        private Activity HandleSystemMessage(Activity message)
        {
            if (message.Type == ActivityTypes.DeleteUserData)
            {
                // Implement user deletion here
                // If we handle user deletion, return a real message
            }
            else if (message.Type == ActivityTypes.ConversationUpdate)
            {
                // Handle conversation state changes, like members being added and removed
                // Use Activity.MembersAdded and Activity.MembersRemoved and Activity.Action for info
                // Not available in all channels
                
            }
            else if (message.Type == ActivityTypes.ContactRelationUpdate)
            {
                // Handle add/remove from contact lists
                // Activity.From + Activity.Action represent what happened
            }
            else if (message.Type == ActivityTypes.Typing)
            {
                // Handle knowing tha the user is typing
            }
            else if (message.Type == ActivityTypes.Ping)
            {
            }

            return null;
        }
    }
}