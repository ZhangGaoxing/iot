﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using UnitsNet;

namespace Iot.Device.Mhz19b
{
    /// <summary>
    /// Add documentation here
    /// </summary>
    public sealed partial class Mhz19b : IDisposable
    {
        private enum CommonMessageBytes
        {
            Start = 0xff,
            SensorNumber = 0x01,
            Empty = 0x00
        }

        private enum Command : byte
        {
            ReadCo2Concentration = 0x86,
            CalibrateZeroPoint = 0x87,
            CalibrateSpanPoint = 0x88,
            AutoCalibrationSwitch = 0x79,
            DetectionRangeSetting = 0x99
        }

        private enum MessageFormat
        {
            Start = 0x00,
            SensorNum = 0x01,
            Command = 0x02,
            DataHigh = 0x03,
            DataLow = 0x04,
            Checksum = 0x08
        }

        private const int MessageSize = 9;

        private bool _disposed = false;
        private SerialPort _serialPort = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="Mhz19b"/> class.
        /// </summary>
        /// <param name="uartDevice">Path to the UART device / serial port, e.g. /dev/serial0</param>
        public Mhz19b(string uartDevice)
        {
            if (uartDevice == null)
            {
                throw new ArgumentNullException(nameof(uartDevice));
            }

            // create serial port using the setting acc. to datasheet, pg. 7, sec. general settings
            _serialPort = new SerialPort(uartDevice, 9600, Parity.None, 8, StopBits.One);
            _serialPort.Encoding = Encoding.ASCII;
            _serialPort.ReadTimeout = 1000;
            _serialPort.WriteTimeout = 1000;
        }

        /// <summary>
        /// Gets the current CO2 concentration from the sensor.
        /// The validity is true if the current concentration was successfully read.
        /// If the serial communication timed out or the checksum was invalid the validity is false.
        /// If the validity is false the ratio is set to 0.
        /// </summary>
        /// <returns>CO2 concentration in ppm and validity</returns>
        public (Ratio, bool) GetCo2Reading()
        {
            Ratio concentration = Ratio.FromPartsPerMillion(0);
            bool validity = false;

            try
            {
                var request = CreateRequest(Command.ReadCo2Concentration);
                request[(int)MessageFormat.Checksum] = Checksum(request);

                _serialPort.Open();
                _serialPort.Write(request, 0, request.Length);

                byte[] response = new byte[MessageSize];
                if ((_serialPort.Read(response, 0, response.Length) == response.Length) && (response[(int)MessageFormat.Checksum] == Checksum(response)))
                {
                    concentration = Ratio.FromPartsPerMillion((int)response[(int)MessageFormat.DataHigh] * 256 + (int)response[(int)MessageFormat.DataLow]);
                    validity = true;
                }
            }
            finally
            {
                _serialPort.Close();
            }

            return (concentration, validity);
        }

        /// <summary>
        /// Initiates a zero point calibration.
        /// The sensor doesn't respond anything, so this is fire and forget.
        /// </summary>
        /// <returns>true, if the command could be send</returns>
        public bool ZeroPointCalibration() => SendRequest(CreateRequest(Command.CalibrateZeroPoint));

        /// <summary>
        /// Initiate a span point calibration.
        /// The sensor doesn't respond anything, so this is fire and forget.
        /// </summary>
        /// <param name="span">span value, e.g. 2000[ppm]</param>
        /// <returns>true, if the command could be send</returns>///
        public bool SpanPointCalibration(int span)
        {
            var request = CreateRequest(Command.CalibrateSpanPoint);
            // set span in request, c. f. datasheet rev. 1.0, pg. 8 for details
            request[(int)MessageFormat.DataHigh] = (byte)(span / 256);
            request[(int)MessageFormat.DataLow] = (byte)(span % 256);

            return SendRequest(request);
        }

        /// <summary>
        /// Switches the autmatic baseline correction on and off.
        /// The sensor doesn't respond anything, so this is fire and forget.
        /// </summary>
        /// <param name="state">State of automatic correction</param>
        /// <returns>true, if the command could be send</returns>
        public bool AutomaticBaselineCorrection(AbmState state)
        {
            var request = CreateRequest(Command.AutoCalibrationSwitch);
            // set on/off state in request, c. f. datasheet rev. 1.0, pg. 8 for details
            request[(int)MessageFormat.DataHigh] = (byte)state;

            return SendRequest(request);
        }

        /// <summary>
        /// Set the sensor detection range.
        /// The sensor doesn't respond anything, so this is fire and forget
        /// </summary>
        /// <param name="detectionRange">Detection range of the sensor</param>
        /// <returns>true, if the command could be send</returns>
        public bool SensorDetectionRange(DetectionRange detectionRange)
        {
            var request = CreateRequest(Command.DetectionRangeSetting);
            // set detection range in request, c. f. datasheet rev. 1.0, pg. 8 for details
            request[(int)MessageFormat.DataHigh] = (byte)((int)detectionRange / 256);
            request[(int)MessageFormat.DataLow] = (byte)((int)detectionRange % 256);

            return SendRequest(request);
        }

        private bool SendRequest(byte[] request)
        {
            bool validity = false;

            request[(int)MessageFormat.Checksum] = Checksum(request);

            try
            {
                _serialPort.Open();
                _serialPort.Write(request, 0, request.Length);
                validity = true;
            }
            finally
            {
                _serialPort.Close();
            }

            return validity;
        }

        private byte[] CreateRequest(Command command) => new byte[]
            {
                (byte)CommonMessageBytes.Start, (byte)CommonMessageBytes.SensorNumber, (byte)command,
                (byte)CommonMessageBytes.Empty, (byte)CommonMessageBytes.Empty, (byte)CommonMessageBytes.Empty,
                (byte)CommonMessageBytes.Empty, (byte)CommonMessageBytes.Empty, (byte)CommonMessageBytes.Empty
            };

        /// <summary>
        /// Calculate checksum for requests and responses.
        /// For details refer to datasheet rev. 1.0, pg. 8.
        /// </summary>
        /// <param name="packet">Packet the checksum is calculated for</param>
        /// <returns>Cheksum</returns>
        private byte Checksum(byte[] packet)
        {
            byte checksum = 0;
            for (int i = 1; i < 8; i++)
            {
                checksum += packet[i];
            }

            checksum = (byte)(0xff - checksum);
            checksum += 1;
            return checksum;
        }

        /// <inheritdoc cref="IDisposable" />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_serialPort != null)
            {
                _serialPort.Dispose();
                _serialPort = null;
                _disposed = true;
            }
        }
    }
}