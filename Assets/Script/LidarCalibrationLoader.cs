using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PPYY.Lidar
{
    // 以前のペーパーヨーヨーコンテンツ(guideStar / Pin TerritoryのFileIO.cs)と同じ形式の
    // 外部ファイルからLIDARキャリブレーション用の四隅の座標を読み込み、LidarSensorBridgeへ反映する。
    //
    // ファイル形式（1行、括弧・カンマは無視して数値だけを抽出するので多少崩れても読める）:
    //   (x0,y0),(x1,y1),(x2,y2),(x3,y3)
    // 例: (1.5,1),(1.5,-1),(0.2,-1),(0.2,1)
    //
    // 壁面に投影した矩形はセンサー座標系で軸に沿った長方形になる前提のため、
    // 実際に使うのは2つのX値・2つのY値だけ（guideStar版FileIO.csのsetCornerと同じ組み方）
    public class LidarCalibrationLoader : MonoBehaviour
    {
        [Tooltip("StreamingAssets フォルダからの相対パス")]
        public string fileName = "LidarCalibration.csv";

        [Tooltip("実行中にこのキーを押すとファイルを再読込し、キャリブレーションを反映し直す")]
        public KeyCode reloadKey = KeyCode.I;

        string FullPath => Path.Combine(Application.streamingAssetsPath, fileName);

        void Start()
        {
            LoadFromFile();
        }

        void Update()
        {
            if (Input.GetKeyDown(reloadKey))
            {
                LoadFromFile();
            }
        }

        void LoadFromFile()
        {
            string path = FullPath;
            if (!File.Exists(path))
            {
                Debug.LogWarning($"LIDARキャリブレーションファイルが見つかりません: {path}");
                return;
            }

            try
            {
                string text;
                using (var sr = new StreamReader(File.OpenRead(path), Encoding.UTF8))
                {
                    text = sr.ReadToEnd();
                }

                var matches = Regex.Matches(text, @"[-+]?[0-9]*\.?[0-9]+");
                var points = new List<float>();
                foreach (Match m in matches)
                {
                    if (float.TryParse(m.Value, out float value)) points.Add(value);
                }

                if (points.Count < 5)
                {
                    Debug.LogWarning($"LIDARキャリブレーションファイルの数値が不足しています: {path}");
                    return;
                }

                // guideStar/Pin TerritoryのFileIO.csと同じ並び。
                // corner0=(x0,y0), corner1=(x0,y1), corner2=(x2,y1), corner3=(x2,y0)
                var corners = new Vector2[4];
                corners[0] = new Vector2(points[0], points[1]);
                corners[1] = new Vector2(points[0], points[3]);
                corners[2] = new Vector2(points[4], points[3]);
                corners[3] = new Vector2(points[4], points[1]);

                if (LidarSensorBridge.Instance != null)
                {
                    LidarSensorBridge.Instance.SetSensorCorners(corners);
                    Debug.Log($"LIDARキャリブレーションを読み込みました: {path}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"LIDARキャリブレーションファイルの読み込みに失敗しました: {e}");
            }
        }
    }
}
