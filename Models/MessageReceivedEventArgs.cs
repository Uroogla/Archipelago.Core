using Archipelago.MultiClient.Net.MessageLog.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Models
{
    public class MessageReceivedEventArgs : EventArgs
    {
        public MessageReceivedEventArgs(LogMessage msg)
        {
            Message = msg;
        }
        LogMessage Message { get; set; }
    }
}
