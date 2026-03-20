using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QL_HieuSuat.AI
{
    public class TaskDelayLinearRegressionService
    {
        private readonly MLContext mlContext;
        private ITransformer? model;
        private PredictionEngine<TaskDelayRegressionInput, TaskDelayRegressionPrediction>? predictionEngine;
        private readonly string modelPath = "taskDelayLinearModel.zip";

        public TaskDelayLinearRegressionService()
        {
            mlContext = new MLContext(seed: 42);

            if (File.Exists(modelPath))
            {
                model = mlContext.Model.Load(modelPath, out _);
                predictionEngine = mlContext.Model.CreatePredictionEngine<TaskDelayRegressionInput, TaskDelayRegressionPrediction>(model);
            }
        }

        public void Train(List<TaskDelayRegressionInput> data)
        {
            if (data == null || data.Count < 10)
            {
                throw new Exception("Cần ít nhất 10 công việc để huấn luyện mô hình hồi quy tuyến tính.");
            }

            var trainData = mlContext.Data.LoadFromEnumerable(data);
            var split = mlContext.Data.TrainTestSplit(trainData, testFraction: 0.2);

            var pipeline = mlContext.Transforms.Concatenate(
                    "Features",
                    nameof(TaskDelayRegressionInput.DoKho),
                    nameof(TaskDelayRegressionInput.DoUuTien),
                    nameof(TaskDelayRegressionInput.SoNguoiThamGia),
                    nameof(TaskDelayRegressionInput.SoNgayConLaiDenHan),
                    nameof(TaskDelayRegressionInput.TiLeHoanThanh),
                    nameof(TaskDelayRegressionInput.TiLeTreHanDuAn))
                .Append(mlContext.Transforms.NormalizeMinMax("Features"))
                .Append(mlContext.Regression.Trainers.Sdca());

            model = pipeline.Fit(split.TrainSet);

            var predictions = model.Transform(split.TestSet);
            var metrics = mlContext.Regression.Evaluate(predictions);

            Console.WriteLine($"[LinearRegression] R2: {metrics.RSquared:0.000}");
            Console.WriteLine($"[LinearRegression] RMSE: {metrics.RootMeanSquaredError:0.000}");

            mlContext.Model.Save(model, trainData.Schema, modelPath);
            predictionEngine = mlContext.Model.CreatePredictionEngine<TaskDelayRegressionInput, TaskDelayRegressionPrediction>(model);
        }

        public float PredictDaysLate(TaskDelayRegressionInput input)
        {
            if (predictionEngine == null)
            {
                throw new Exception("Mô hình hồi quy tuyến tính chưa được huấn luyện.");
            }

            var prediction = predictionEngine.Predict(input).SoNgayTreDuDoan;
            return Math.Max(0f, prediction);
        }

        public static double ConvertDaysLateToRiskProbability(double predictedDaysLate)
        {
            if (predictedDaysLate <= 0)
            {
                return 0;
            }

            // Logistic scaling so risk remains in [0,1] and increases quickly after 2-3 late days.
            var value = 1.0 / (1.0 + Math.Exp(-0.55 * (predictedDaysLate - 2.0)));
            return Math.Clamp(value, 0, 1);
        }
    }
}
