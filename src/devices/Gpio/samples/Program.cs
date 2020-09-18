﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using Iot.Device.Gpio.Drivers;
using System.Device.Gpio;
using System.Threading;

namespace AllwinnerGpioDriver.Samples
{
    class Program
    {
        static void Main(string[] args)
        {
            int pinNum = 7;
            using GpioController gpio = new GpioController(PinNumberingScheme.Board, new OrangePiZeroDriver());

            gpio.OpenPin(pinNum);
            gpio.SetPinMode(pinNum, PinMode.Output);

            for (int i = 0; i < 10; i++)
            {
                gpio.Write(pinNum, PinValue.High);
                Thread.Sleep(500);
                gpio.Write(pinNum, PinValue.Low);
                Thread.Sleep(500);
            }
        }
    }
}
