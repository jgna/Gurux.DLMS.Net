//
// --------------------------------------------------------------------------
//  Gurux Ltd
// 
//
//
// Filename:        $HeadURL$
//
// Version:         $Revision$,
//                  $Date$
//                  $Author$
//
// Copyright (c) Gurux Ltd
//
//---------------------------------------------------------------------------
//
//  DESCRIPTION
//
// This file is a part of Gurux Device Framework.
//
// Gurux Device Framework is Open Source software; you can redistribute it
// and/or modify it under the terms of the GNU General Public License 
// as published by the Free Software Foundation; version 2 of the License.
// Gurux Device Framework is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of 
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. 
// See the GNU General Public License for more details.
//
// More information of Gurux products: http://www.gurux.org
//
// This code is licensed under the GNU General Public License v2. 
// Full text may be retrieved at http://www.gnu.org/licenses/gpl-2.0.txt
//---------------------------------------------------------------------------


namespace Gurux.DLMS
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Gurux.DLMS.Objects;
    using Gurux.DLMS.Enums;
    using Gurux.DLMS.Secure;
    using Gurux.DLMS.Internal;

    /// <summary> 
    /// This class includes DLMS communication settings.
    /// </summary>
    public class GXDLMSSettings
    {
        ///<summary>
        /// Server frame sequence starting number. 
        ///</summary>
        private const byte ServerStartFrameSequence = 0x0F;

        ///<summary>
        /// Client frame sequence starting number. 
        ///</summary>
        private const byte ClientStartFrameSequence = 0xEE;

        ///<summary>
        /// Default Max received PDU size.
        ///</summary>
        private const UInt16 DefaultMaxReceivePduSize = 0xFFFF;

        /// <summary>
        /// Invoke ID.
        /// </summary>
        private byte invokeID = 0x1;

        /// <summary>
        /// Invoke ID.
        /// </summary>
        internal UInt32 longInvokeID = 0x1;

        ///<summary>
        ///Client to server challenge.
        ///</summary>
        private byte[] ctoSChallenge;

        ///<summary>
        ///Server to Client challenge. 
        ///</summary>
        private byte[] stoCChallenge;

        /// <summary>
        /// HDLC sender frame sequence number.
        /// </summary>
        private byte SenderFrame;

        /// <summary>
        /// HDLC receiver frame sequence number.
        /// </summary>
        private byte ReceiverFrame;

        /// <summary>
        /// Is this server or client.
        /// </summary>
        private bool Server;

        /// <summary>
        /// Source system title.
        /// </summary>
        internal byte[] SourceSystemTitle;

        /// <summary>
        /// Key Encrypting Key, also known as Master key.
        /// </summary>
        internal byte[] Kek;

        /// <summary>
        /// Long data count.
        /// </summary>
        internal UInt16 Count;

        /// <summary>
        /// Long data index.
        /// </summary>
        internal UInt16 Index;       

        /// <summary>
        /// Cipher interface.
        /// </summary>
        /// <remarks>
        /// GXDLMSAssociationShortName and GXDLMSAssociationLogicalName use this is GMAC authentication is used.
        /// </remarks>
        internal GXICipher Cipher
        {
            get;
            set;
        }

        ///<summary>
        ///Constructor. 
        ///</summary>
        internal GXDLMSSettings(bool isServer)
        {
            UseCustomChallenge = false;
            StartingBlockIndex = BlockIndex = 1;
            DLMSVersion = 6;
            InvokeID = 0x1;
            Priority = Priority.High;
            ServiceClass = ServiceClass.UnConfirmed;
            MaxReceivePDUSize = DefaultMaxReceivePduSize;
            Server = isServer;
            Objects = new GXDLMSObjectCollection();
            Limits = new GXDLMSLimits();
            LnSettings = new GXDLMSLNSettings(new byte[] { 0x00, 0xFE, 0x1F });
            SnSettings = new GXDLMSSNSettings(new byte[] { 0x1C, 0x03, 0x20 });
            ResetFrameSequence();
        }

        /// <summary>
        /// Client to Server challenge. 
        /// </summary>
        public byte[] CtoSChallenge
        {
            get
            {
                return ctoSChallenge;
            }
            set
            {
                if (!UseCustomChallenge || ctoSChallenge == null)
                {
                    ctoSChallenge = value;
                }
            }
        }

        /// <summary>
        /// Server to Client challenge.         
        /// </summary>
        public byte[] StoCChallenge
        {
            get
            {
                return stoCChallenge;
            }
            set
            {
                if (!UseCustomChallenge || stoCChallenge == null)
                {
                    stoCChallenge = value;
                }
            }
        }

        /// <summary>
        /// Used authentication. 
        /// </summary>
        public Authentication Authentication
        {
            get;
            set;
        }

        /// <summary>
        /// Client password.
        /// </summary>
        public byte[] Password
        {
            get;
            set;
        }

        /// <summary>
        /// DLMS version number.
        /// </summary>
        public byte DlmsVersionNumber
        {
            get;
            internal set;
        }

        internal bool Connected = false;

        ///<summary>
        /// Increase receiver sequence.
        ///</summary>
        ///<param name="value">
        /// Frame value.
        ///</param>
        ///<returns>
        /// Increased receiver frame sequence.
        ///</returns>
        static byte IncreaseReceiverSequence(byte value)
        {
            return (byte)(value + 0x20 | 0x10 | value & 0xE);
        }

        ///<summary>
        ///Increase sender sequence.
        ///</summary>
        ///<param name="value">
        /// Increase frame value. 
        ///</param>
        ///<returns> 
        /// Increased sender frame sequence. 
        ///</returns>
        static byte IncreaseSendSequence(byte value)
        {
            return (byte)(value & 0xF0 | (value + 0x2) & 0xE);
        }

        ///<summary>
        ///Reset frame sequence. 
        ///</summary>
        public void ResetFrameSequence()
        {
            if (IsServer)
            {
                SenderFrame = 0x1E;
                ReceiverFrame = 0xEE;
            }
            else
            {
                SenderFrame = 0x10;
                ReceiverFrame = 0xE;
            }
        }

        public bool CheckFrame(byte frame)
        {
            //If U frame.
            if ((frame & (byte)HdlcFrameType.Uframe) == (byte)HdlcFrameType.Uframe)
            {
                if (frame == 0x73 || frame == 0x93)
                {
                    ResetFrameSequence();
                    return true;
                }
            }
            //If S -frame.
            if ((frame & (byte)HdlcFrameType.Sframe) == (byte)HdlcFrameType.Sframe)
            {
                ReceiverFrame = IncreaseReceiverSequence(ReceiverFrame);
                return true;
            }
            //Handle I-frame.
            if (frame == (byte) IncreaseReceiverSequence(IncreaseSendSequence(ReceiverFrame)))
            {
                ReceiverFrame = frame;
                return true;
            }
            //If answer for RR.
            if (frame == (byte)IncreaseSendSequence(ReceiverFrame))
            {
                ReceiverFrame = frame;
                return true;
            }
            System.Diagnostics.Debug.WriteLine("Frame ID do not match.");
            return false;
        }

        ///<summary>
        /// Generates I-frame. 
        ///</summary>
        internal byte NextSend()
        {
            SenderFrame = IncreaseReceiverSequence(IncreaseSendSequence(SenderFrame));
            return SenderFrame;
        }
      
        ///<summary>
        ///Generates Receiver Ready S-frame. 
        ///</summary>
        internal byte ReceiverReady()
        {
            SenderFrame = (byte)(IncreaseReceiverSequence(SenderFrame) | 1);
            return (byte)(SenderFrame & 0xF1);           
        }

        ///<summary>
        ///Generates Keep Alive S-frame. 
        ///</summary>
        internal byte KeepAlive()
        {
            SenderFrame = (byte)(SenderFrame | 1);
            return (byte)(SenderFrame & 0xF1);
        }

        ///<summary>
        /// Gets Logical Name settings. 
        ///</summary>
        public GXDLMSLNSettings LnSettings
        {
            get;
            private set;
        }

        ///<summary>
        /// Short name settings.
        ///</summary>
        public GXDLMSSNSettings SnSettings
        {
            get;
            private set;
        }

        ///<summary>
        ///Current block index. 
        public UInt32 BlockIndex
        {
            get;
            internal set;
        }

        /// <summary>
        ///  Gets starting block index. Default is One based, but some meters use Zero based value. 
        ///  Usually this is not used.
        /// </summary>
        public UInt32 StartingBlockIndex
        {
            get;
            internal set;
        }


        ///<summary>
        /// Resets block index to default value.
        ///</summary>
        internal void ResetBlockIndex()
        {
            BlockIndex = StartingBlockIndex;
        }

        ///<summary>
        /// Increases block index.
        ///</summary>
        internal void IncreaseBlockIndex()
        {
            BlockIndex += 1;
        }

        /// <summary>
        /// Is this server or client settings.
        /// </summary>
        internal bool IsServer
        {
            get
            {
                return Server;
            }
        }

        /// <summary>
        /// Information from the frame size that server can handle.
        /// </summary>
        public GXDLMSLimits Limits
        {
            get;
            internal set;
        }

        /// <summary>
        /// Used interface.
        /// </summary>
        public InterfaceType InterfaceType
        {
            get;
            set;
        }

        /// <summary>
        /// Client address.
        /// </summary>
        public int ClientAddress
        {
            get;
            set;
        }

        /// <summary>
        /// Server address.
        /// </summary>
        public int ServerAddress
        {
            get;
            set;
        }

        /// <summary>
        /// Size of Server address.
        /// </summary>
        public byte ServerAddressSize
        {
            get;
            set;
        }

        /// <summary>
        /// DLMS version number.
        /// </summary>
        public byte DLMSVersion
        {
            get;
            set;
        }

        /// <summary>
        /// Maximum PDU size.
        /// </summary>
        public UInt16 MaxReceivePDUSize
        {
            get;
            set;
        }

        /// <summary>
        /// Is Logical Name Referencing used.
        /// </summary>
        public bool UseLogicalNameReferencing
        {
            get;
            set;
        }

        /// <summary>
        /// Used priority.
        /// </summary>
        public Priority Priority
        {
            get;
            set;
        }

        /// <summary>
        /// Used service class.
        /// </summary>
        public ServiceClass ServiceClass
        {
            get;
            set;
        }

        /// <summary>
        /// Invoke ID.
        /// </summary>
        public byte InvokeID
        {
            get
            {
                return invokeID;
            }
            set
            {
                if (value > 0xF)
                {
                    throw new System.ArgumentException("Invalid InvokeID");
                }
                invokeID = value;
            }
        }

        /// <summary>
        /// Collection of the objects.
        /// </summary>
        public GXDLMSObjectCollection Objects
        {
            get;
            internal set;
        }

        ///<summary>
        /// Is custom challenges used. If custom challenge is used new challenge is
        /// not generated if it is set. This is for debugging purposes. 
        ///</summary>
        internal bool UseCustomChallenge
        {
            get;
            set;
        }
    }
}