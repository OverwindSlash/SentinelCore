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

            definition.Name = "HK-Demo";

            definition.IsObjectAnalyzableRetain = false;
            definition.IsDoubleLineCounting = false;

            int width = 3632;
            int height = 1632;

            definition.SetImageSize(width, height);


            // tracking area
            {
                AnalysisArea analysisArea = new AnalysisArea();
                analysisArea.Name = "alarm region";

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
                // analysisArea.AddPoint(topLeft);
                // analysisArea.AddPoint(topRight);
                // analysisArea.AddPoint(bottomRight);
                // analysisArea.AddPoint(bottomLeft);

                // method 2
                analysisArea.AddPoint(new NormalizedPoint(width, height, 1452, 656));
                analysisArea.AddPoint(new NormalizedPoint(width, height, 2461, 656));
                analysisArea.AddPoint(new NormalizedPoint(width, height, 2461, 987));
                analysisArea.AddPoint(new NormalizedPoint(width, height, 1452, 987));

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
                lane.AddPoint(new NormalizedPoint(width, height, 1611, 945));
                lane.AddPoint(new NormalizedPoint(width, height, 1626, 749));
                lane.AddPoint(new NormalizedPoint(width, height, 2073, 809));
                lane.AddPoint(new NormalizedPoint(width, height, 2080, 975));
                lane.AddPoint(new NormalizedPoint(width, height, 2104, 976));
                lane.AddPoint(new NormalizedPoint(width, height, 2115, 814));
                lane.AddPoint(new NormalizedPoint(width, height, 2388, 836));
                lane.AddPoint(new NormalizedPoint(width, height, 2328, 854));
                lane.AddPoint(new NormalizedPoint(width, height, 2345, 879));
                lane.AddPoint(new NormalizedPoint(width, height, 2371, 886));
                lane.AddPoint(new NormalizedPoint(width, height, 2358, 905));
                lane.AddPoint(new NormalizedPoint(width, height, 2365, 946));
                lane.AddPoint(new NormalizedPoint(width, height, 2295, 982));
                lane.AddPoint(new NormalizedPoint(width, height, 2150, 982));
                lane.AddPoint(new NormalizedPoint(width, height, 2152, 965));
                lane.AddPoint(new NormalizedPoint(width, height, 2138, 964));
                lane.AddPoint(new NormalizedPoint(width, height, 2134, 981));
                lane.AddPoint(new NormalizedPoint(width, height, 1823, 981));

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
