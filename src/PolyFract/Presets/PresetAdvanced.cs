using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using PolyFract.Gui;

namespace PolyFract.Presets
{
    public class PresetAdvanced : BasePreset
    {
        public override string Name => "Advanced";

        public override int Order => 10;

        public override double DT => 0.0005;

        public override double Intensity => 1.0;

        private List<PointOfView> povMovement = [];

        private List<CoefficientTimePoint> coeffMovement = [];

        public PresetAdvanced()
        {
            AddTimePoint(0, new Complex(0, 0.006666666666666667), 300.0, [new Complex(-1, 0), new Complex(1, 0)]);
            AddTimePoint(0.1, new Complex(0.0033333333333333335, -0.0033333333333333335), 300.0, [new Complex(-0.9866666666666668, 0.15999999999999992), new Complex(1, 0)]);
            AddTimePoint(0.2, new Complex(-0.03333333333333333, 0), 300.0, [new Complex(-0.9366666666666672, 0.33000000000000007), new Complex(1, 0)]);
            AddTimePoint(0.3, new Complex(0.29704663769300554, 0.08575420257141966), 337.8, [new Complex(-0.9366666666666672, 0.33000000000000007), new Complex(1, 0)]);
            AddTimePoint(0.4, new Complex(0.5119606097283301, 0.15403550476944883), 354.2, [new Complex(-0.9366666666666672, 0.33000000000000007), new Complex(0.9822364316059974, 0.17467508920769137)]);
            AddTimePoint(0.5, new Complex(0.498818152521622, 0.20375865429491485), 505.5, [new Complex(-0.9564490994703331, 0.35769540592513277), new Complex(0.9763017017648974, 0.214239954815024)]);
            AddTimePoint(0.6, new Complex(0.6239400070791883, 0.2635189992187019), 686.5, [new Complex(-0.5059801350407129, 0.3383043577874348), new Complex(1.0615545343994328, 0.17303441904166394)]);
            AddTimePoint(0.7, new Complex(0.5547300837705103, 0.24461594097511113), 912.0, [new Complex(-0.5074367943058646, 0.6820759443632272), new Complex(0.9625017043691172, -0.35136291641293277)]);
            AddTimePoint(0.8, new Complex(0.08763346276707253, 0.6185125319661253), 912.0, [new Complex(-0.4317802993546032, 0.9792195694616581), new Complex(0.9625017043691172, -0.35136291641293277)]);
            AddTimePoint(0.9, new Complex(0.5210771753890346, 0.8573770800851499), 1177.7, [new Complex(0.027695764392987352, 0.7219289912446468), new Complex(0.9763017017648974, 0.214239954815024)]);
            AddTimePoint(1.0, new Complex(0.49390611219624225, 0.8760571860301948), 1177.7, [new Complex(0.3588430970551448, 0.8144804252450959), new Complex(0.9763017017648974, 0.214239954815024)]);
            AddTimePoint(1.1, new Complex(0.08414948207678538, 0.5591821502730672), 805.0, [new Complex(0.3588430970551448, 0.8144804252450959), new Complex(0.6074127980410521, -0.03762212427918749)]);
            AddTimePoint(1.2, new Complex(0.00026752200829374684, 0.002177783352446727), 528.8, [new Complex(0.3588430970551448, 0.8144804252450959), new Complex(0.2764989142865578, -0.08111366328692099)]);
            AddTimePoint(1.3, new Complex(-0.013935956592638687, 0.022697363113174218), 301.5, [new Complex(0.3588430970551448, 0.8144804252450959), new Complex(0.07752514881135447, -0.260190052214604)]);
            AddTimePoint(1.4, new Complex(0.029528961029753956, 0.031969654523676616), 293.1, [new Complex(-0.21086344517482442, 0.739429263993243), new Complex(-0.01117167812264963, -0.23972155369137127)]);
        }

        private void AddTimePoint(double time, Complex origin, double zoom, Complex[] coeffs)
        {
            if (povMovement.Count == 0)
                time = 0;
            povMovement.Add(new PointOfView(origin, zoom, time));
            coeffMovement.Add(new CoefficientTimePoint(coeffs, time));
        }

        public override PointOfView GetPOV(double t) => GetInterpolated(povMovement, t);

        public override Complex[] GetCoefficients(double t) => GetInterpolated(coeffMovement, t);
    }
}
