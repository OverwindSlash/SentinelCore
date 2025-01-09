using SentinelCore.Domain.Entities.AnalysisDefinitions;
using SentinelCore.Domain.Entities.AnalysisDefinitions.Geometrics;

namespace SentinelCore.Domain.Tests.AnalysisEngine
{
    [TestFixture]
    public class ImageAnalysisDefinitionTests
    {
        [Test]
        public void TestCreateDefinitionFile()
        {
            var definition = new ImageAnalysisDefinition();

            definition.Name = "Suzhou-Demo";

            definition.IsObjectAnalyzableRetain = false;
            definition.IsDoubleLineCounting = false;

            int width = 2560;
            int height = 1440;

            definition.SetImageSize(width, height);


            // tracking area
            {
                AnalysisArea analysisArea = new AnalysisArea();
                analysisArea.Name = "alarm region";

                // method 1
                var topLeft = new NormalizedPoint(0, 0);
                var topRight = new NormalizedPoint(1, 0);
                var bottomRight = new NormalizedPoint(1, 1);
                var bottomLeft = new NormalizedPoint(0, 1);
                topLeft.SetImageSize(width, height);
                topRight.SetImageSize(width, height);
                bottomRight.SetImageSize(width, height);
                bottomLeft.SetImageSize(width, height);
                
                analysisArea.AddPoint(topLeft);
                analysisArea.AddPoint(topRight);
                analysisArea.AddPoint(bottomRight);
                analysisArea.AddPoint(bottomLeft);

                // method 2
                // analysisArea.AddPoint(new NormalizedPoint(width, height, 66, 634));
                // analysisArea.AddPoint(new NormalizedPoint(width, height, 254, 1110));
                // analysisArea.AddPoint(new NormalizedPoint(width, height, 1930, 417));
                // analysisArea.AddPoint(new NormalizedPoint(width, height, 1155, 367));

                definition.AddAnalysisArea(analysisArea);
            }

            // excluded area
            {
                ExcludedArea excludedArea = new ExcludedArea();
                excludedArea.Name = "osd region";

                // method 1
                var topLeft = new NormalizedPoint(0, 0);
                var topRight = new NormalizedPoint(1, 0);
                var bottomRight = new NormalizedPoint(1, 1);
                var bottomLeft = new NormalizedPoint(0, 1);
                topLeft.SetImageSize(width, height);
                topRight.SetImageSize(width, height);
                bottomRight.SetImageSize(width, height);
                bottomLeft.SetImageSize(width, height);

                excludedArea.AddPoint(topLeft);
                excludedArea.AddPoint(topRight);
                excludedArea.AddPoint(bottomRight);
                excludedArea.AddPoint(bottomLeft);

                // method 2
                excludedArea.AddPoint(new NormalizedPoint(width, height, 0, 0));
                excludedArea.AddPoint(new NormalizedPoint(width, height, width, 0));
                excludedArea.AddPoint(new NormalizedPoint(width, height, width, height));
                excludedArea.AddPoint(new NormalizedPoint(width, height, 0, height));

                // definition.AddExcludedArea(excludedArea);
            }

            // lane 1
            {
                Lane lane = new Lane();
                lane.Name = "alarm region";
                lane.Type = "alarm";
                lane.Index = 1;

                // method 1
                // var topLeft = new NormalizedPoint(0, 0);
                // var topRight = new NormalizedPoint(1, 0);
                // var bottomRight = new NormalizedPoint(1, 1);
                // var bottomLeft = new NormalizedPoint(0, 1);
                // topLeft.SetImageSize(width, height);
                // topRight.SetImageSize(width, height);
                // bottomRight.SetImageSize(width, height);
                // bottomLeft.SetImageSize(width, height);
                //
                // lane.AddPoint(topLeft);
                // lane.AddPoint(topRight);
                // lane.AddPoint(bottomRight);
                // lane.AddPoint(bottomLeft);

                // method 2
                lane.AddPoint(new NormalizedPoint(width, height, 66, 634));
                lane.AddPoint(new NormalizedPoint(width, height, 254, 1110));
                lane.AddPoint(new NormalizedPoint(width, height, 1930, 417));
                lane.AddPoint(new NormalizedPoint(width, height, 1155, 367));

                definition.Lanes.Add(lane);
            }

            // count line (upward)
            {
                // enter line
                EnterLine enterLine = new EnterLine();
                enterLine.Name = "count line (enter)";

                // method 1
                var enterLineStart = new NormalizedPoint(0, 0);
                var enterLineStop = new NormalizedPoint(1, 0);
                enterLineStart.SetImageSize(width, height);
                enterLineStop.SetImageSize(width, height);

                enterLine.Start = enterLineStart;
                enterLine.Stop = enterLineStop;

                // method 2
                enterLine.Start = new NormalizedPoint(width, height, 0, 0);
                enterLine.Stop = new NormalizedPoint(width, height, width, 0);
                
                // leave line
                LeaveLine leaveLine = new LeaveLine();
                leaveLine.Name = "count line (leave)";

                // method 1
                var leaveLineStart = new NormalizedPoint(0, 0);
                var leaveLineStop = new NormalizedPoint(1, 0);
                leaveLineStart.SetImageSize(width, height);
                leaveLineStop.SetImageSize(width, height);

                leaveLine.Start = leaveLineStart;
                leaveLine.Stop = leaveLineStop;


                // method 2
                leaveLine.Start = new NormalizedPoint(width, height, width, height);
                leaveLine.Stop = new NormalizedPoint(width, height, width, 0);
                
                // definition.CountLines.Add(new Tuple<EnterLine, LeaveLine>(enterLine, leaveLine));
            }

            ImageAnalysisDefinition.SaveToJson("test.json", definition);
        }
    }
}
