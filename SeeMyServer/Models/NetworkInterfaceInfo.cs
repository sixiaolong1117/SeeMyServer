using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeeMyServer.Models
{
    public class NetworkInterfaceInfo
    {
        public string Interface { get; set; }
        public string ReceiveBytes { get; set; }
        public decimal ReceiveBytesOrigin { get; set; }
        public string ReceivePackets { get; set; }
        public decimal ReceiveSpeedByte { get; set; }
        public string ReceiveSpeed { get; set; }
        public string TransmitBytes { get; set; }
        public decimal TransmitBytesOrigin { get; set; }
        public string TransmitPackets { get; set; }
        public decimal TransmitSpeedByte { get; set; }
        public string TransmitSpeed { get; set; }
    }
}
