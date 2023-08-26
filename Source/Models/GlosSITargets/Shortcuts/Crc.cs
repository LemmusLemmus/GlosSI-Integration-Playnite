// CRC calculation utility. "pycrc" originally written by Thomas Pircher in Python.
// The code has been translated to C# and stripped of some features.
// See below for the original Python code copyright notice.
//
// Copyright (c) 2006-2013  Thomas Pircher  <tehpeh@gmx.net>
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to
// deal in the Software without restriction, including without limitation the
// rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
// sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
// IN THE SOFTWARE.

using System;

namespace GlosSIIntegration.Models.GlosSITargets.Shortcuts
{
    /// <summary>
    /// A base class for CRC routines.
    /// </summary>
    public class Crc
    {
        private readonly int width;
        private readonly bool reflectIn, reflectOut;
        private readonly uint xorOut, poly;
        private readonly uint msbMask, mask;
        private readonly uint nonDirectInit;

        public Crc(int width, uint poly, bool reflectIn, uint xorIn, bool reflectOut, uint xorOut)
        {
            this.width = width;
            this.poly = poly;
            this.reflectIn = reflectIn;
            this.reflectOut = reflectOut;
            this.xorOut = xorOut;
            this.msbMask = 0x1U << this.width - 1;
            this.mask = this.msbMask - 1 << 1 | 1;
            this.nonDirectInit = GetNondirectInit(xorIn);
        }

        /// <summary>
        /// Returns the non-direct init if the direct algorithm has been selected.
        /// </summary>
        private uint GetNondirectInit(uint init)
        {
            uint crc = init;

            for (int i = 0; i < this.width; i++)
            {
                uint bit = crc & 0x01;
                if (bit != 0)
                {
                    crc ^= this.poly;
                }
                crc >>= 1;
                if (bit != 0)
                {
                    crc |= this.msbMask;
                }
            }
            return crc & this.mask;
        }
   
        /// <summary>
        /// Reflect a data word, i.e. reverts the bit order.
        /// </summary>
        public uint Reflect(uint data, int width)
        {
            uint x = data & 0x01;

            for (int i = 0; i < width - 1; i++)
            {
                data >>= 1;
                x = x << 1 | data & 0x01;
            }
            return x;
        }
   
        /// <summary>
        /// Classic simple and slow CRC implementation. 
        /// This function iterates bit by bit over the augmented input message and returns 
        /// the calculated CRC value at the end.
        /// </summary>
        public uint BitByBit(string input)
        {
            bool topbit;
            uint register = this.nonDirectInit;

            foreach (char c in input)
            {
                uint octet = c;
                if (this.reflectIn) octet = Reflect(octet, 8);
                for (int i = 0; i < 8; i++)
                {
                    topbit = Convert.ToBoolean(register & this.msbMask);
                    register = register << 1 & this.mask | octet >> 7 - i & 0x01;
                    if (topbit)
                    {
                        register ^= this.poly;
                    }
                }
            }
            for (int i = 0; i < this.width; i++)
            {
                topbit = Convert.ToBoolean(register & this.msbMask);
                register = register << 1 & this.mask;
                if (topbit)
                {
                    register ^= this.poly;
                }
            }
            if (this.reflectOut) register = Reflect(register, this.width);
            return register ^ this.xorOut;
        }
    }
}