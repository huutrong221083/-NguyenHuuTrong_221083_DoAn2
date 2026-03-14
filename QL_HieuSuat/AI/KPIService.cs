using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.IO;

namespace QL_HieuSuat.AI
{
    public class KPIService
    {
        private MLContext mlContext;

        private ITransformer model;

        private PredictionEngine<KPIModel, KPIPrediction> predictionEngine;

        private string modelPath = "kpiModel.zip";

        public KPIService()
        {
            mlContext = new MLContext();

            // Nếu model đã tồn tại thì load
            if (File.Exists(modelPath))
            {
                model =
                mlContext.Model.Load(
                modelPath,
                out var schema);

                predictionEngine =
                mlContext.Model.CreatePredictionEngine
                <KPIModel, KPIPrediction>(model);
            }
        }

        public void Train(List<KPIModel> data)
        {
            var trainData =
            mlContext.Data.LoadFromEnumerable(data);

            // chia train test
            var split =
            mlContext.Data.TrainTestSplit(
            trainData,
            testFraction: 0.2);

            var pipeline =

            mlContext.Transforms.Concatenate(
            "Features",

            nameof(KPIModel.Sonamkinhnghiem),

            nameof(KPIModel.SoCongViec),

            nameof(KPIModel.DoKhoTrungBinh),

            nameof(KPIModel.DoUuTienTrungBinh),

            nameof(KPIModel.TyLeTreHan))

            .Append(
            mlContext.Transforms.NormalizeMinMax("Features"))

            .Append(
            mlContext.Regression.Trainers.FastTree(
            numberOfTrees: 100,
            numberOfLeaves: 20,
            minimumExampleCountPerLeaf: 5));

            model =
            pipeline.Fit(split.TrainSet);

            // test model
            var predictions =
            model.Transform(split.TestSet);

            var metrics =
            mlContext.Regression.Evaluate(predictions);

            Console.WriteLine(
            "R2 score: " + metrics.RSquared);

            Console.WriteLine(
            "RMSE: " + metrics.RootMeanSquaredError);

            // SAVE MODEL (điểm cộng lớn)
            mlContext.Model.Save(
            model,
            trainData.Schema,
            modelPath);

            predictionEngine =
            mlContext.Model.CreatePredictionEngine
            <KPIModel, KPIPrediction>(model);
        }

        public float Predict(KPIModel input)
        {
            // nếu chưa train thì lỗi
            if (predictionEngine == null)
                throw new Exception("Model chưa train");

            var result =
            predictionEngine.Predict(input);

            return result.PredictedKPI;
        }
    }
}