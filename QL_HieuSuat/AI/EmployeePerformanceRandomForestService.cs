using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QL_HieuSuat.AI
{
    public class EmployeePerformanceRandomForestService
    {
        private readonly MLContext mlContext;
        private ITransformer? model;
        private PredictionEngine<EmployeePerformanceClassificationInput, EmployeePerformanceClassificationPrediction>? predictionEngine;
        private readonly string modelPath = "employeePerformanceRfModel.zip";

        public EmployeePerformanceRandomForestService()
        {
            mlContext = new MLContext(seed: 42);

            if (File.Exists(modelPath))
            {
                model = mlContext.Model.Load(modelPath, out _);
                predictionEngine = mlContext.Model.CreatePredictionEngine<EmployeePerformanceClassificationInput, EmployeePerformanceClassificationPrediction>(model);
            }
        }

        public EmployeePerformanceTrainingResult Train(List<EmployeePerformanceClassificationInput> data)
        {
            if (data == null || data.Count < 12)
            {
                throw new Exception("Cần ít nhất 12 nhân viên để huấn luyện mô hình Random Forest.");
            }

            var distinctLabels = data.Select(x => x.XepLoai).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (distinctLabels.Count < 2)
            {
                throw new Exception("Dữ liệu huấn luyện cần ít nhất 2 nhóm xếp loại khác nhau.");
            }

            var trainData = mlContext.Data.LoadFromEnumerable(data);
            var split = mlContext.Data.TrainTestSplit(trainData, testFraction: 0.2);

            var featureColumns = GetFeatureColumns();

            var pipeline = mlContext.Transforms.Conversion.MapValueToKey("Label")
                .Append(mlContext.Transforms.Concatenate("Features", featureColumns))
                .Append(mlContext.Transforms.NormalizeMinMax("Features"))
                .Append(mlContext.MulticlassClassification.Trainers.OneVersusAll(
                    mlContext.BinaryClassification.Trainers.FastForest(numberOfLeaves: 20, numberOfTrees: 200, minimumExampleCountPerLeaf: 2)))
                .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            model = pipeline.Fit(split.TrainSet);

            var transformedTest = model.Transform(split.TestSet);
            var metrics = mlContext.MulticlassClassification.Evaluate(transformedTest);

            Console.WriteLine($"[RandomForestClassifier] MacroAccuracy: {metrics.MacroAccuracy:0.000}");
            Console.WriteLine($"[RandomForestClassifier] MicroAccuracy: {metrics.MicroAccuracy:0.000}");

            mlContext.Model.Save(model, trainData.Schema, modelPath);
            predictionEngine = mlContext.Model.CreatePredictionEngine<EmployeePerformanceClassificationInput, EmployeePerformanceClassificationPrediction>(model);

            var importance = ComputeFeatureImportance(data);

            return new EmployeePerformanceTrainingResult
            {
                MacroAccuracy = metrics.MacroAccuracy,
                MicroAccuracy = metrics.MicroAccuracy,
                FeatureImportance = importance
            };
        }

        public EmployeePerformancePredictionResult Predict(EmployeePerformanceClassificationInput input)
        {
            if (predictionEngine == null)
            {
                throw new Exception("Mô hình Random Forest chưa được huấn luyện.");
            }

            var prediction = predictionEngine.Predict(input);
            var score = prediction.Score?.Length > 0 ? prediction.Score.Max() : 0f;

            return new EmployeePerformancePredictionResult
            {
                PredictedLabel = prediction.PredictedLabel,
                Confidence = Math.Clamp(score, 0, 1)
            };
        }

        private static Dictionary<string, double> ComputeFeatureImportance(List<EmployeePerformanceClassificationInput> data)
        {
            if (data.Count == 0)
            {
                return new Dictionary<string, double>();
            }

            var labelValues = data.Select(x => LabelToOrdinal(x.XepLoai)).ToList();

            var raw = new Dictionary<string, List<double>>
            {
                [nameof(EmployeePerformanceClassificationInput.Sonamkinhnghiem)] = data.Select(x => (double)x.Sonamkinhnghiem).ToList(),
                [nameof(EmployeePerformanceClassificationInput.SoCongViec)] = data.Select(x => (double)x.SoCongViec).ToList(),
                [nameof(EmployeePerformanceClassificationInput.DoKhoTrungBinh)] = data.Select(x => (double)x.DoKhoTrungBinh).ToList(),
                [nameof(EmployeePerformanceClassificationInput.DoUuTienTrungBinh)] = data.Select(x => (double)x.DoUuTienTrungBinh).ToList(),
                [nameof(EmployeePerformanceClassificationInput.TyLeTreHan)] = data.Select(x => (double)x.TyLeTreHan).ToList(),
                [nameof(EmployeePerformanceClassificationInput.TyLeHoanThanh)] = data.Select(x => (double)x.TyLeHoanThanh).ToList(),
                [nameof(EmployeePerformanceClassificationInput.DiemKpiGanNhat)] = data.Select(x => (double)x.DiemKpiGanNhat).ToList()
            };

            var importance = raw
                .ToDictionary(kv => kv.Key, kv => Math.Abs(CalculatePearsonCorrelation(kv.Value, labelValues)))
                .OrderByDescending(kv => kv.Value)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            return importance;
        }

        private static double LabelToOrdinal(string label)
        {
            return label switch
            {
                "Xuất sắc" => 4,
                "Tốt" => 3,
                "Trung bình" => 2,
                "Yếu" => 1,
                _ => 0
            };
        }

        private static double CalculatePearsonCorrelation(IReadOnlyList<double> x, IReadOnlyList<double> y)
        {
            if (x.Count != y.Count || x.Count == 0)
            {
                return 0;
            }

            var n = x.Count;
            var avgX = x.Average();
            var avgY = y.Average();

            double numerator = 0;
            double sumSqX = 0;
            double sumSqY = 0;

            for (var i = 0; i < n; i++)
            {
                var dx = x[i] - avgX;
                var dy = y[i] - avgY;
                numerator += dx * dy;
                sumSqX += dx * dx;
                sumSqY += dy * dy;
            }

            var denominator = Math.Sqrt(sumSqX * sumSqY);
            if (denominator <= 0)
            {
                return 0;
            }

            return numerator / denominator;
        }

        private static string[] GetFeatureColumns()
        {
            return
            [
                nameof(EmployeePerformanceClassificationInput.Sonamkinhnghiem),
                nameof(EmployeePerformanceClassificationInput.SoCongViec),
                nameof(EmployeePerformanceClassificationInput.DoKhoTrungBinh),
                nameof(EmployeePerformanceClassificationInput.DoUuTienTrungBinh),
                nameof(EmployeePerformanceClassificationInput.TyLeTreHan),
                nameof(EmployeePerformanceClassificationInput.TyLeHoanThanh),
                nameof(EmployeePerformanceClassificationInput.DiemKpiGanNhat)
            ];
        }
    }

    public class EmployeePerformanceTrainingResult
    {
        public double MacroAccuracy { get; set; }

        public double MicroAccuracy { get; set; }

        public Dictionary<string, double> FeatureImportance { get; set; } = new();
    }

    public class EmployeePerformancePredictionResult
    {
        public string PredictedLabel { get; set; } = string.Empty;

        public double Confidence { get; set; }
    }
}
