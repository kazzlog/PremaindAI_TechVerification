﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using TMPro;
using UnityEngine;

namespace PreMaid.RemoteController
{
    /// <summary>
    /// 普通のラジコンぽく動かすサンプルスクリプト
    /// </summary>
    public class PreMaidPoseController : MonoBehaviour
    {
        [SerializeField] List<PreMaidServo> _servos = new List<PreMaidServo>();

        private string portName = "COM7";
        private const int BaudRate = 115200;
        private SerialPort _serialPort;

        [SerializeField] private bool _serialPortOpen = false;


        private bool _continuousMode = false;


        //何秒ごとにポーズ指定するか
        private float _poseProcessDelay = 0.25f;

        private float _timer = 0.0f;


        [SerializeField] private TMPro.TMP_Dropdown _dropdown = null;

        [SerializeField] private ServoUguiController _uguiController = null;

        // Start is called before the first frame update
        void Start()
        {
            _servos.Clear();
            PreMaidServo.AllServoPositionDump();
            foreach (PreMaidServo.ServoPosition item in Enum.GetValues(typeof(PreMaidServo.ServoPosition)))
            {
                PreMaidServo servo = new PreMaidServo(item);

                _servos.Add(servo);
            }

            //一覧を出す
            foreach (var VARIABLE in _servos)
            {
                Debug.Log(VARIABLE.GetServoIdString() + "   " + VARIABLE.GetServoId() + "  サーボ数値変換" +
                          VARIABLE.GetServoIdAndValueString());
            }

            _uguiController.Initialize(_servos);
            _uguiController.OnChangeValue += OnChangeValue;

            Debug.Log(BuildPoseString());
            var portNames = SerialPort.GetPortNames();

            if (_dropdown == null)
            {
                Debug.LogError("シリアルポートを選択するDropDownが指定されていません");
                return;
            }

            List<TMP_Dropdown.OptionData> serialPortNamesList = new List<TMP_Dropdown.OptionData>();

            foreach (var VARIABLE in portNames)
            {
                TMP_Dropdown.OptionData optionData = new TMP_Dropdown.OptionData(VARIABLE);
                serialPortNamesList.Add(optionData);

                Debug.Log(VARIABLE);
            }

            _dropdown.ClearOptions();
            _dropdown.AddOptions(serialPortNamesList);
        }

        private void OnChangeValue()
        {
            //Debug.Log("値の変更");
            //Refreshします～
            var latestValues = _uguiController.GetCurrenSliderValues();

            for (int i = 0; i < _servos.Count; i++)
            {
                _servos[i].SetServoValueSafeClamp((int) latestValues[i]);
            }
        }


        public void SetContinuousMode(bool newValue)
        {
            _continuousMode = newValue;
            if (newValue)
            {
                _timer = 0;
            }
        }


        public void OpenSerialPort()
        {
            Debug.Log(_dropdown.options[_dropdown.value].text + "を開きます");

            _serialPortOpen = SerialPortOpen(_dropdown.options[_dropdown.value].text);
        }

        /// <summary>
        /// シリアルポートを開ける
        /// </summary>
        /// <returns></returns>
        bool SerialPortOpen(string portName)
        {
            try
            {
                _serialPort = new SerialPort(portName, BaudRate, Parity.None, 8, StopBits.One);
                _serialPort.Open();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("シリアルポートOpen失敗しました、ペアリング済みか、プリメイドAIのポートか確認してください");
                Console.WriteLine(e);
                return false;
            }


            Debug.LogWarning($"指定された{portName}がありません。portNameを書き換えてください");
            return false;
        }


        /// <summary>
        /// 現在のサーボ値を適用する1フレームだけのモーションを送る
        /// </summary>
        /// <returns></returns>
        string BuildPoseString(int speed = 50)
        {
            if (speed > 255)
            {
                speed = 255;
            }

            if (speed < 1)
            {
                speed = 1;
            }

            //決め打ちのポーズ命令+スピード(小さい方が速くて、255が最大に遅い)
            string ret = "50 18 00 " + speed.ToString("X2");
            //そして各サーボぼ値を入れる
            foreach (var VARIABLE in _servos)
            {
                ret += " " + VARIABLE.GetServoIdAndValueString();
            }

            ret += " FF"; //パリティビットを仮で挿入する;

            //パリティビットを計算し直した値にして、文字列を返す
            return PreMaidUtility.RewriteXorString(ret);
        }


        /// <summary>
        /// 現在のサーボ値を適用してシリアル通信でプリメイドAI実機に送る
        /// </summary>
        public void ApplyPose()
        {
            if (_serialPortOpen == false)
            {
                return;
            }
            
            StartCoroutine(ApplyPoseCoroutine());
        }


        /// <summary>
        /// たぶんあとで非同期待ち受けつかう
        /// </summary>
        /// <returns></returns>
        IEnumerator ApplyPoseCoroutine()
        {
            float waitSec = 0.06f; //0.03だと送信失敗することがある

            //ここでポーズ情報を取得する
            byte[] willSendPoseBytes =
                PreMaidUtility.BuildByteDataFromStringOrder(
                    BuildPoseString(80)); //対象のモーション、今回は1個だけ

            _serialPort.Write(willSendPoseBytes, 0, willSendPoseBytes.Length);
            yield return new WaitForSeconds(waitSec);
        }


        // Update is called once per frame
        void Update()
        {
            if (_serialPortOpen == false)
            {
                return;
            }

            if (_continuousMode == false)
            {
                return;
            }

            _timer += Time.deltaTime;
            if (_timer > _poseProcessDelay)
            {
                ApplyPose();
                _timer -= _poseProcessDelay;
            }
        }
    }
}