/*
This file came from Managed Media Aggregation, You can always find the latest version @ https://net7mma.codeplex.com/
  
 Julius.Friedman@gmail.com / (SR. Software Engineer ASTI Transportation Inc. http://www.asti-trans.com)

Permission is hereby granted, free of charge, 
 * to any person obtaining a copy of this software and associated documentation files (the "Software"), 
 * to deal in the Software without restriction, 
 * including without limitation the rights to :
 * use, 
 * copy, 
 * modify, 
 * merge, 
 * publish, 
 * distribute, 
 * sublicense, 
 * and/or sell copies of the Software, 
 * and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
 * 
 * 
 * JuliusFriedman@gmail.com should be contacted for further details.

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
 * 
 * IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
 * DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, 
 * TORT OR OTHERWISE, 
 * ARISING FROM, 
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 * 
 * v//
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Media.Rtsp.Server.MediaTypes
{
    /// <summary>
    /// Sends System.Drawing.Images over Rtp by encoding them as a RFC2435 Jpeg
    /// </summary>
    public class RFC2435Media : RtpSink
    {

        #region NestedTypes

        /// <summary>
        /// Implements RFC2435
        /// Encodes from a System.Drawing.Image to a RFC2435 Jpeg.
        /// Decodes a RFC2435 Jpeg to a System.Drawing.Image.
        ///  <see cref="http://tools.ietf.org/rfc/rfc2435.txt">RFC 2435</see>        
        ///  <see cref="http://en.wikipedia.org/wiki/JPEG"/>
        /// </summary>
        public class RFC2435Frame : Rtp.RtpFrame
        {
            #region Statics

            const int JpegMaxSizeDimension = 65500; //65535

            //public const int MaxWidth = 2048;

            //public const int MaxHeight = 4096;

            //RFC2435 Section 3.1.4 and 3.1.5

            public const int MaxWidth = 2040;

            public const int MaxHeight = 2040;

            public const byte RtpJpegPayloadType = 26;

            internal static System.Drawing.Imaging.ImageCodecInfo JpegCodecInfo = System.Drawing.Imaging.ImageCodecInfo.GetImageDecoders().First(d => d.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);

            /// <summary>
            /// Creates RST header for JPEG/RTP packet.
            /// </summary>
            /// <param name="dri">dri Restart interval - number of MCUs between restart markers</param>
            /// <param name="f">optional first bit (defaults to 1)</param>
            /// <param name="l">optional last bit (defaults to 1)</param>
            /// <param name="count">optional number of restart markers (defaults to 0x3FFF)</param>
            /// <returns>Rst Marker</returns>
            [CLSCompliant(false)]
            public static byte[] CreateRtpJpegDataRestartIntervalMarker(ushort dri, bool f = true, bool l = true, ushort count = 0x3FFF)
            {
                //     0                   1                   2                   3
                //0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
                //+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
                //|       Restart Interval        |F|L|       Restart Count       |
                //+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
                byte[] data = new byte[4];
                data[0] = (byte)((dri >> 8) & 0xFF);
                data[1] = (byte)dri;

                //Network ByteOrder            

                Media.Common.Binary.Write16(data, 2, BitConverter.IsLittleEndian, count);

                if (f) data[2] = (byte)((1) << 7);

                if (l) data[2] |= (byte)((1) << 6);

                return data;
            }

            public static byte[] CreateRtpJpegDataRestartIntervalMarker(short dri, bool f = true, bool l = true, short count = 0x3FFF)
            {
                return CreateRtpJpegDataRestartIntervalMarker((ushort)dri, f, l, (ushort)count);
            }

            /// <summary>
            /// Utility function to create RtpJpegHeader either for initial packet or template for further packets
            /// </summary>
            /// <param name="typeSpecific"></param>
            /// <param name="fragmentOffset"></param>
            /// <param name="jpegType"></param>
            /// <param name="quality"></param>
            /// <param name="width"></param>
            /// <param name="height"></param>
            /// <param name="dri"></param>
            /// <param name="qTables"></param>
            /// <returns></returns>
            public static byte[] CreateRtpJpegHeader(int typeSpecific, long fragmentOffset, int jpegType, int quality, int width, int height, byte[] dri, byte precisionTable, List<byte> qTables)
            {
                List<byte> RtpJpegHeader = new List<byte>();

                /*
                0                   1                   2                   3
                0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
                +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
                | Type-specific |              Fragment Offset                  |
                +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
                |      Type     |       Q       |     Width     |     Height    |
                +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
                */

                //Type specific
                //http://tools.ietf.org/search/rfc2435#section-3.1.1
                RtpJpegHeader.Add((byte)typeSpecific);

                //Three byte fragment offset
                //http://tools.ietf.org/search/rfc2435#section-3.1.2

                //Common.Binary.WriteNetwork24()
                //Common.Binary.GetBytes(fragmentOffset, BitConverter.IsLittleEndian)

                if (BitConverter.IsLittleEndian) fragmentOffset = Common.Binary.ReverseU32((uint)fragmentOffset);

                Media.Common.Extensions.List.ListExtensions.AddRange(RtpJpegHeader, BitConverter.GetBytes((uint)fragmentOffset), 1, 3);


                //(Jpeg)Type
                //http://tools.ietf.org/search/rfc2435#section-3.1.3
                RtpJpegHeader.Add((byte)jpegType);

                //http://tools.ietf.org/search/rfc2435#section-3.1.4 (Q)
                RtpJpegHeader.Add((byte)quality);

                //http://tools.ietf.org/search/rfc2435#section-3.1.5 (Width)
                RtpJpegHeader.Add((byte)(((width + 7) & ~7) >> 3));

                //http://tools.ietf.org/search/rfc2435#section-3.1.6 (Height)
                RtpJpegHeader.Add((byte)(((height + 7) & ~7) >> 3));

                //If this is the first packet
                if (fragmentOffset == 0)
                {
                    //http://tools.ietf.org/search/rfc2435#section-3.1.7 (Restart Marker header)
                    if (jpegType >= 63 && dri != null)
                    {
                        //Create a Rtp Restart Marker, Set first and last
                        RtpJpegHeader.AddRange(CreateRtpJpegDataRestartIntervalMarker(Common.Binary.ReadU16(dri, 0, BitConverter.IsLittleEndian)));
                    }

                    //Handle Quantization Tables if provided
                    if (quality >= 100)
                    {
                        int qTablesCount = qTables.Count;

                        //Check for a table
                        if (qTablesCount < 64) throw new InvalidOperationException("At least 1 quantization table must be included when quality >= 100");

                        //Check for overflow
                        if (qTablesCount > ushort.MaxValue) Common.Binary.CreateOverflowException("qTables", qTablesCount, ushort.MinValue.ToString(), ushort.MaxValue.ToString());

                        RtpJpegHeader.Add(0); //Must Be Zero      

                        RtpJpegHeader.Add(precisionTable);//PrecisionTable may be bit flagged to indicate 16 bit tables

                        //Add the Length field
                        if (BitConverter.IsLittleEndian) RtpJpegHeader.AddRange(BitConverter.GetBytes(Common.Binary.ReverseU16((ushort)qTablesCount)));
                        else RtpJpegHeader.AddRange(BitConverter.GetBytes((ushort)qTablesCount));

                        //here qTables may have 16 bit precision and may need to be reversed if BitConverter.IsLittleEndian
                        RtpJpegHeader.AddRange(qTables);
                    }
                }

                return RtpJpegHeader.ToArray();
            }



            // The default 'luma' and 'chroma' quantizer tables, in zigzag order and energy reduced
            static byte[] defaultQuantizers = new byte[]
        {
           // luma table:
           16, 11, 12, 14, 12, 10, 16, 14,
           13, 14, 18, 17, 16, 19, 24, 40,
           26, 24, 22, 22, 24, 49, 35, 37,
           29, 40, 58, 51, 61, 60, 57, 51,
           56, 55, 64, 72, 92, 78, 64, 68,
           87, 69, 55, 56, 80, 109, 81, 87,
           95, 98, 103, 104, 103, 62, 77, 113,
           121, 112, 100, 120, 92, 101, 103, 99,
           // chroma table:
           17, 18, 18, 24, 21, 24, 47, 26,
           26, 47, 99, 66, 56, 66, 99, 99,
           99, 99, 99, 99, 99, 99, 99, 99,
           99, 99, 99, 99, 99, 99, 99, 99,
           99, 99, 99, 99, 99, 99, 99, 99,
           99, 99, 99, 99, 99, 99, 99, 99,
           99, 99, 99, 99, 99, 99, 99, 99,
           99, 99, 99, 99, 99, 99, 99, 99
        };

            static byte[] rfcQuantizers = new byte[]
        {
           // luma table:
            //From RFC2435 / Jpeg Spec
            16, 11, 10, 16, 24, 40, 51, 61,
            12, 12, 14, 19, 26, 58, 60, 55,
            14, 13, 16, 24, 40, 57, 69, 56,
            14, 17, 22, 29, 51, 87, 80, 62,
            18, 22, 37, 56, 68, 109, 103, 77,
            24, 35, 55, 64, 81, 104, 113, 92,
            49, 64, 78, 87, 103, 121, 120, 101,
            72, 92, 95, 98, 112, 100, 103, 99,
           // chroma table:
            //From RFC2435 / Jpeg Spec
            17, 18, 24, 47, 99, 99, 99, 99,
            18, 21, 26, 66, 99, 99, 99, 99,
            24, 26, 56, 99, 99, 99, 99, 99,
            47, 66, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99
        };

            /// <summary>
            /// Creates a Luma and Chroma Table in ZigZag order using the default quantizers specified in RFC2435
            /// </summary>
            /// <param name="Q">The quality factor</param>
            /// <returns>64 luma bytes and 64 chroma</returns>
            internal static byte[] CreateQuantizationTables(uint type, uint Q, byte precision, bool useRfcQuantizer)
            {
                if (Q >= 100) throw new InvalidOperationException("For Q >= 100, a dynamically defined quantization table is used, which might be specified by a session setup protocol.");

                byte[] quantizer = useRfcQuantizer ? rfcQuantizers : defaultQuantizers;

                //Factor restricted to range of 1 and 99
                int factor = (int)Math.Min(Math.Max(1, Q), 99);

                //Seed quantization value
                int q = (Q >= 1 && Q <= 50 ? (int)(5000 / factor) : 200 - factor * 2);

                //Create 2 quantization tables from Seed quality value using the RFC quantizers
                int tableSize = quantizer.Length / 2;
                byte[] resultTables = new byte[tableSize * 2];
                for (int lumaIndex = 0, chromaIndex = tableSize; lumaIndex < tableSize; ++lumaIndex, ++chromaIndex)
                {
                    //8 Bit tables
                    if (precision == 0)
                    {
                        //Clamp with Min, Max (Should be left in tact but endian is unknown on receiving side)
                        //Luma
                        resultTables[lumaIndex] = (byte)Math.Min(Math.Max((quantizer[lumaIndex] * q + 50) / 100, 1), byte.MaxValue);
                        //Chroma
                        resultTables[chromaIndex] = (byte)Math.Min(Math.Max((quantizer[chromaIndex] * q + 50) / 100, 1), byte.MaxValue);
                    }
                    else //16 bit tables
                    {
                        //Luma
                        if (BitConverter.IsLittleEndian)
                            BitConverter.GetBytes(Common.Binary.ReverseU16((ushort)Math.Min(Math.Max((quantizer[lumaIndex] * q + 50) / 100, 1), byte.MaxValue))).CopyTo(resultTables, lumaIndex++);
                        else
                            BitConverter.GetBytes((ushort)Math.Min(Math.Max((quantizer[lumaIndex] * q + 50) / 100, 1), byte.MaxValue)).CopyTo(resultTables, lumaIndex++);

                        //Chroma
                        if (BitConverter.IsLittleEndian)
                            BitConverter.GetBytes(Common.Binary.ReverseU16((ushort)Math.Min(Math.Max((quantizer[chromaIndex] * q + 50) / 100, 1), byte.MaxValue))).CopyTo(resultTables, chromaIndex++);
                        else
                            BitConverter.GetBytes((ushort)Math.Min(Math.Max((quantizer[chromaIndex] * q + 50) / 100, 1), byte.MaxValue)).CopyTo(resultTables, chromaIndex++);
                    }
                }

                return resultTables;
            }



            //Lumiance

            //JpegHuffmanTable StdDCLuminance

            static byte[] lum_dc_codelens = { 0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0 },
                //Progressive
                        lum_dc_codelens_p = { 0, 2, 3, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

            static byte[] lum_dc_symbols = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
                //Progressive
                        lum_dc_symbols_p = { 0, 2, 3, 0, 1, 4, 5, 6, 7 }; //lum_dc_symbols_p = { 0, 0, 2, 1, 3, 4, 5, 6, 7}; Work for TestProg but not TestImgP

            //JpegHuffmanTable StdACLuminance

            static byte[] lum_ac_codelens = { 0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 0x7d };

            static byte[] lum_ac_symbols = 
            {
                0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12,
                0x21, 0x31, 0x41, 0x06, 0x13, 0x51, 0x61, 0x07,
                0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xa1, 0x08,
                0x23, 0x42, 0xb1, 0xc1, 0x15, 0x52, 0xd1, 0xf0,
                0x24, 0x33, 0x62, 0x72, 0x82, 0x09, 0x0a, 0x16,
                0x17, 0x18, 0x19, 0x1a, 0x25, 0x26, 0x27, 0x28,
                0x29, 0x2a, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39,
                0x3a, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49,
                0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59,
                0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69,
                0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79,
                0x7a, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89,
                0x8a, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98,
                0x99, 0x9a, 0xa2, 0xa3, 0xa4, 0xa5, 0xa6, 0xa7,
                0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4, 0xb5, 0xb6,
                0xb7, 0xb8, 0xb9, 0xba, 0xc2, 0xc3, 0xc4, 0xc5,
                0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2, 0xd3, 0xd4,
                0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda, 0xe1, 0xe2,
                0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9, 0xea,
                0xf1, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8,
                0xf9, 0xfa
            };

            //Chromiance

            //JpegHuffmanTable StdDCChrominance
            static byte[] chm_dc_codelens = { 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0 },
                //Progressive
                        chm_dc_codelens_p = { 0, 3, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

            static byte[] chm_dc_symbols = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
                //Progressive
                        chm_dc_symbols_p = { 0, 1, 2, 3, 0, 4, 5 };

            //JpegHuffmanTable StdACChrominance

            static byte[] chm_ac_codelens = { 0, 2, 1, 2, 4, 4, 3, 4, 7, 5, 4, 4, 0, 1, 2, 0x77 };

            static byte[] chm_ac_symbols = 
            {
                0x00, 0x01, 0x02, 0x03, 0x11, 0x04, 0x05, 0x21,
                0x31, 0x06, 0x12, 0x41, 0x51, 0x07, 0x61, 0x71,
                0x13, 0x22, 0x32, 0x81, 0x08, 0x14, 0x42, 0x91,
                0xa1, 0xb1, 0xc1, 0x09, 0x23, 0x33, 0x52, 0xf0,
                0x15, 0x62, 0x72, 0xd1, 0x0a, 0x16, 0x24, 0x34,
                0xe1, 0x25, 0xf1, 0x17, 0x18, 0x19, 0x1a, 0x26,
                0x27, 0x28, 0x29, 0x2a, 0x35, 0x36, 0x37, 0x38,
                0x39, 0x3a, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48,
                0x49, 0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58,
                0x59, 0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68,
                0x69, 0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78,
                0x79, 0x7a, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87,
                0x88, 0x89, 0x8a, 0x92, 0x93, 0x94, 0x95, 0x96,
                0x97, 0x98, 0x99, 0x9a, 0xa2, 0xa3, 0xa4, 0xa5,
                0xa6, 0xa7, 0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4,
                0xb5, 0xb6, 0xb7, 0xb8, 0xb9, 0xba, 0xc2, 0xc3,
                0xc4, 0xc5, 0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2,
                0xd3, 0xd4, 0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda,
                0xe2, 0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9,
                0xea, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8,
                0xf9, 0xfa
            };

            //http://www.hackerfactor.com/src/jpegquality.c

            /// <summary>
            /// Experimentally determine a Quality factor form the given tables.
            /// </summary>
            /// <param name="precisionTable"></param>
            /// <param name="tables"></param>
            /// <param name="offset"></param>
            /// <param name="length"></param>
            /// <returns></returns>
            public static int DetermineQuality(byte precisionTable, byte[] tables, int offset, int length)
            {
                //Average from all tables

                //See also http://trac.imagemagick.org/browser/ImageMagick/trunk/coders/jpeg.c @ JPEGSetImageQuality

                int tableCount = length / (precisionTable > 0 ? 128 : 64);

                if (length % tableCount > 0) tableCount = 1;

                int tableSize = length / tableCount;

                int total = 0, diff = 0;

                for (int i = 0; i < tableCount; ++i)
                {
                    diff = tables.Skip(i * tableCount).Take(tableSize).Skip(1).Sum(b => b);
                    total += diff;
                    diff = total - diff;
                }

                return diff == 0 ? 100 : (int)100.0 - total / diff;

            }



            #endregion

            #region Constructor

            static RFC2435Frame() { if (JpegCodecInfo == null) throw new NotSupportedException("The system must have a Jpeg Codec installed."); }

            /// <summary>
            /// Creates an empty JpegFrame
            /// </summary>
            public RFC2435Frame() : base(RFC2435Frame.RtpJpegPayloadType) { MaxPackets = 2048; }

            /// <summary>
            /// Creates a new JpegFrame from an existing RtpFrame which has the JpegFrame PayloadType
            /// </summary>
            /// <param name="f">The existing frame</param>
            public RFC2435Frame(Rtp.RtpFrame f) : base(f) { if (PayloadTypeByte != RFC2435Frame.RtpJpegPayloadType) throw new ArgumentException("Expected the payload type 26, Found type: " + f.PayloadTypeByte); }

            /// <summary>
            /// Creates a shallow copy an existing JpegFrame
            /// </summary>
            /// <param name="f">The JpegFrame to copy</param>
            public RFC2435Frame(RFC2435Frame f) : this((Rtp.RtpFrame)f) { Buffer = f.Buffer; }


            #endregion

            #region Fields

            /// <summary>
            /// Provied access the to underlying buffer where the image is stored.
            /// </summary>
            public System.IO.MemoryStream Buffer { get; protected set; }

            #endregion

            #region Properties

            public override bool IsComplete
            {
                get
                {
                    if (false == base.IsComplete) return false;

                    var packet = m_Packets.First().Value;

                    return Common.Binary.ReadU24(packet.Payload, packet.HeaderOctets + 1, BitConverter.IsLittleEndian) == 0;
                }
            }

            #endregion

            #region Methods


            //Todo - Remove 'Image'

            //Overload Buffer to PrepareBuffer.



            public override void Dispose()
            {
                if (IsDisposed) return;

                //Call dispose on the base class
                base.Dispose();

                //Dispose the buffer
                DisposeBuffer();
            }

            internal void DisposeBuffer()
            {
                if (Buffer != null)
                {
                    Buffer.Dispose();

                    Buffer = null;
                }
            }


            /// <summary>
            /// Removing All Packets in a JpegFrame destroys any Image associated with the Frame
            /// </summary>
            public override void RemoveAllPackets()
            {
                DisposeBuffer();
                base.RemoveAllPackets();
            }

            public override Rtp.RtpPacket Remove(int sequenceNumber)
            {
                DisposeBuffer();
                return base.Remove(sequenceNumber);
            }

            #endregion


        }

        #endregion

        #region Fields

        //Should be moved to SourceStream? Should have Fps and calculate for developers?
        protected int clockRate = 9;//kHz //90 dekahertz

        //Should be moved to SourceStream?
        protected readonly int sourceId = (int)DateTime.UtcNow.Ticks;

        protected Queue<Rtp.RtpFrame> m_Frames = new Queue<Rtp.RtpFrame>();

        //RtpClient so events can be sourced to Clients through RtspServer
        protected Rtp.RtpClient m_RtpClient;

        //Watches for files if given in constructor
        protected System.IO.FileSystemWatcher m_Watcher;

        protected int m_FramesPerSecondCounter = 0;

        #endregion

        static List<string> SupportedImageFormats = new List<string>(System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders().SelectMany(enc => enc.FilenameExtension.Split((char)Common.ASCII.SemiColon)).Select(s => s.Substring(1).ToLowerInvariant()));

        #region Propeties

        public virtual double FramesPerSecond { get { return Math.Max(m_FramesPerSecondCounter, 1) / Math.Abs(Uptime.TotalSeconds); } }

        public virtual int Width { get; protected set; } //EnsureDimensios

        public virtual int Height { get; protected set; }

        public virtual int Quality { get; protected set; }

        public virtual bool Interlaced { get; protected set; }

        //Should also allow payloadsize e.g. BytesPerPacketPayload to be set here?

        /// <summary>
        /// Implementes the SessionDescription property for RtpSourceStream
        /// </summary>
        public override Rtp.RtpClient RtpClient { get { return m_RtpClient; } }

        #endregion

        #region Constructor

        public RFC2435Media(string name, string directory = null, bool watch = true)
            : base(name, new Uri("file://" + System.IO.Path.GetDirectoryName(directory)))
        {

            if (Quality == 0) Quality = 80;

            //If we were told to watch and given a directory and the directory exists then make a FileSystemWatcher
            if (System.IO.Directory.Exists(base.Source.LocalPath) && watch)
            {
                m_Watcher = new System.IO.FileSystemWatcher(base.Source.LocalPath);
                m_Watcher.EnableRaisingEvents = true;
                m_Watcher.NotifyFilter = System.IO.NotifyFilters.CreationTime;
                // m_Watcher.Created += FileCreated;
            }
        }

        public RFC2435Media(string name, string directory, bool watch, int width, int height, bool interlaced, int quality = 80)
            : this(name, directory, watch)
        {
            Width = width;

            Height = height;

            Interlaced = interlaced;

            Quality = quality;

            EnsureDimensions();
        }

        #endregion

        #region Methods

        void EnsureDimensions()
        {
            int over;

            Math.DivRem(Width, Common.Binary.BitsPerByte, out over);

            if (over > 0) Width += over;

            Math.DivRem(Height, Common.Binary.BitsPerByte, out over);

            if (over > 0) Height += over;
        }

        //SourceStream Implementation
        public override void Start()
        {
            if (m_RtpClient != null) return;

            //Create a RtpClient so events can be sourced from the Server to many clients without this Client knowing about all participants
            //If this class was used to send directly to one person it would be setup with the recievers address
            m_RtpClient = new Rtp.RtpClient();

            SessionDescription = new Sdp.SessionDescription(0, "v√ƒ", Name);
            SessionDescription.Add(new Sdp.Lines.SessionConnectionLine()
            {
                ConnectionNetworkType = Sdp.Lines.SessionConnectionLine.InConnectionToken,
                ConnectionAddressType = Sdp.SessionDescription.WildcardString,
                ConnectionAddress = System.Net.IPAddress.Any.ToString()
            });

            //Add a MediaDescription to our Sdp on any available port for RTP/AVP Transport using the RtpJpegPayloadType            
            SessionDescription.Add(new Sdp.MediaDescription(Sdp.MediaType.video, 0, Rtp.RtpClient.RtpAvpProfileIdentifier, RFC2435Media.RFC2435Frame.RtpJpegPayloadType));

            //Indicate control to each media description contained
            SessionDescription.Add(new Sdp.SessionDescriptionLine("a=control:*"));

            //Ensure the session members know they can only receive
            SessionDescription.Add(new Sdp.SessionDescriptionLine("a=sendonly")); //recvonly?

            //that this a broadcast.
            SessionDescription.Add(new Sdp.SessionDescriptionLine("a=type:broadcast"));


            //Add a Interleave (We are not sending Rtcp Packets becaues the Server is doing that) We would use that if we wanted to use this ImageSteam without the server.            
            //See the notes about having a Dictionary to support various tracks
            m_RtpClient.TryAddContext(new Rtp.RtpClient.TransportContext(0, 1, sourceId, SessionDescription.MediaDescriptions.First(), false, 0));

            //Add the control line
            SessionDescription.MediaDescriptions.First().Add(new Sdp.SessionDescriptionLine("a=control:trackID=1"));

            //Add the line with the clock rate in ms, obtained by TimeSpan.TicksPerMillisecond * clockRate            

            //Make the thread
            m_RtpClient.m_WorkerThread = new System.Threading.Thread(SendPackets);
            m_RtpClient.m_WorkerThread.TrySetApartmentState(System.Threading.ApartmentState.MTA);
            //m_RtpClient.m_WorkerThread.IsBackground = true;
            //m_RtpClient.m_WorkerThread.Priority = System.Threading.ThreadPriority.BelowNormal;
            m_RtpClient.m_WorkerThread.Name = "SourceStream-" + Id;

            //If we are watching and there are already files in the directory then add them to the Queue
            if (m_Watcher != null && !string.IsNullOrWhiteSpace(base.Source.LocalPath) && System.IO.Directory.Exists(base.Source.LocalPath))
            {
                foreach (string file in System.IO.Directory.GetFiles(base.Source.LocalPath))
                {

                    if (false == SupportedImageFormats.Any(ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase))) continue;


                }

                //If we have not been stopped already
                if (/*State != StreamState.Started && */ m_RtpClient.m_WorkerThread != null)
                {
                    //Only ready after all pictures are in the queue
                    Ready = true;
                    m_RtpClient.m_WorkerThread.Start();
                }
            }
            else
            {
                //We are ready
                Ready = true;
                m_RtpClient.m_WorkerThread.Start();
            }

            base.Start();
        }

        public override void Stop()
        {
            Ready = false;

            if (m_Watcher != null)
            {
                m_Watcher.EnableRaisingEvents = false;
                //m_Watcher.Created -= FileCreated;
                m_Watcher.Dispose();
                m_Watcher = null;
            }

            if (m_RtpClient != null)
            {
                m_RtpClient.Disconnect();
                m_RtpClient = null;
            }

            m_Frames.Clear();

            SessionDescription = null;

            base.Stop();
        }



        /// <summary>
        /// Add a frame of existing packetized data
        /// </summary>
        /// <param name="frame">The frame with packets to send</param>
        public void AddFrame(Rtp.RtpFrame frame)
        {
            try { m_Frames.Enqueue(frame); }
            catch { throw; }
        }


        //Needs to only send packets and not worry about updating the frame, that should be done by ImageSource

        internal override void SendPackets()
        {

            m_RtpClient.FrameChangedEventsEnabled = false;

            while (State == StreamState.Started)
            {
                try
                {
                    if (m_Frames.Count == 0)
                    {

                        m_RtpClient.m_WorkerThread.Priority = System.Threading.ThreadPriority.Lowest;

                        System.Threading.Thread.Sleep(clockRate);

                        continue;
                    }

                    int period = (clockRate * 1000 / m_Frames.Count);

                    //Dequeue a frame or die
                    Rtp.RtpFrame frame = m_Frames.Dequeue();

                    if (frame == null || frame.IsDisposed) continue;

                    //Get the transportChannel for the packet
                    Rtp.RtpClient.TransportContext transportContext = RtpClient.GetContextBySourceId(frame.SynchronizationSourceIdentifier);

                    //If there is a context
                    if (transportContext != null)
                    {
                        //Increase priority
                        m_RtpClient.m_WorkerThread.Priority = System.Threading.ThreadPriority.AboveNormal;

                        transportContext.RtpTimestamp += period;

                        foreach (Rtp.RtpPacket packet in frame)
                        {
                            //Copy the values before we signal the server
                            //packet.Channel = transportContext.DataChannel;
                            packet.SynchronizationSourceIdentifier = (int)sourceId;
                            packet.Timestamp = (int)transportContext.RtpTimestamp;

                            //Increment the sequence number on the transportChannel and assign the result to the packet
                            packet.SequenceNumber = ++transportContext.SequenceNumber;

                            //Fire an event so the server sends a packet to all clients connected to this source
                            if (false == m_RtpClient.FrameChangedEventsEnabled) RtpClient.OnRtpPacketReceieved(packet);
                        }

                        //Modified packet is no longer complete because SequenceNumbers were modified

                        //Fire a frame changed event manually
                        if (m_RtpClient.FrameChangedEventsEnabled) RtpClient.OnRtpFrameChanged(frame);

                        unchecked { ++m_FramesPerSecondCounter; }
                    }

                    //If we are to loop images then add it back at the end
                    if (Loop)
                    {
                        m_Frames.Enqueue(frame);
                    }

                    System.Threading.Thread.Sleep(clockRate);

                }
                catch (Exception ex)
                {
                    if (ex is System.Threading.ThreadAbortException)
                    {
                        //Handle the abort
                        System.Threading.Thread.ResetAbort();

                        Stop();

                        return;
                    }
                    continue;
                }
            }
        }

        #endregion
    }

    //public sealed class ChildRtpImageSource : ChildStream
    //{
    //    public ChildRtpImageSource(RtpImageSource source) : base(source) { }
    //}
}
