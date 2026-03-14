using Microsoft.ML;
using System.Collections.Generic;
using System.IO;

namespace QL_HieuSuat.AI
{
    public class ProjectAIService
    {
        private MLContext mlContext;
        private ITransformer model;
        private PredictionEngine<ProjectAIModel, ProjectAIPrediction> engine;
        private string modelPath = "projectModel.zip";

        public ProjectAIService()
        {
            mlContext = new MLContext();

            if (File.Exists(modelPath))
            {
                model =
                mlContext.Model.Load(
                modelPath,
                out var schema);

                engine =
                mlContext.Model.CreatePredictionEngine
                <ProjectAIModel, ProjectAIPrediction>(model);
            }
        }

        public void Train(List<ProjectAIModel> data)
        {
            if (data.Count < 5)
                throw new Exception(
                "Cần ít nhất 5 project để train AI");

            int positive =
            data.Count(x => x.DuAnTre);

            int negative =
            data.Count(x => !x.DuAnTre);

            if (positive == 0 || negative == 0)
                throw new Exception(
                "Data phải có cả dự án trễ và không trễ");

            var trainData =
            mlContext.Data.LoadFromEnumerable(data);

            var split =
            mlContext.Data.TrainTestSplit(
            trainData,
            testFraction: 0.2);

            var pipeline =
            mlContext.Transforms.Concatenate(
            "Features",

            nameof(ProjectAIModel.SoCongViec),

            nameof(ProjectAIModel.TyLeHoanThanh),

            nameof(ProjectAIModel.TyLeTreHan),

            nameof(ProjectAIModel.DoKhoTrungBinh),

            nameof(ProjectAIModel.DoUuTienTrungBinh),

            nameof(ProjectAIModel.SoNhanVien))

            .Append(
            mlContext.Transforms
            .NormalizeMinMax("Features"))

            .Append(
            mlContext.BinaryClassification
            .Trainers.FastTree());

            model =
            pipeline.Fit(split.TrainSet);

            var predictions =
            model.Transform(split.TestSet);

            try
            {
                var metrics =
                mlContext.BinaryClassification
                .Evaluate(predictions);

                Console.WriteLine(
                "Accuracy:" +
                metrics.Accuracy);

                Console.WriteLine(
                "F1:" +
                metrics.F1Score);
            }
            catch
            {
                Console.WriteLine(
                "Test set không đủ label variation");
            }

            engine =
            mlContext.Model
            .CreatePredictionEngine
            <ProjectAIModel,
            ProjectAIPrediction>(model);

            mlContext.Model.Save(

            model,

            trainData.Schema,

            modelPath);
        }

        public float Predict(ProjectAIModel input)
        {
            if (engine == null)
                throw new Exception("Model chưa train");

            var result =
            engine.Predict(input);

            return result.Probability;
        }
    }
}