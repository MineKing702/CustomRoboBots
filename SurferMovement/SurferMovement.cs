using System;
using System.Collections.Generic;
using System.Linq;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;
using Robocode.TankRoyale.BotApi.Graphics;

public class SurferMovement : Bot
{
    // Configuration
    private const int BINS = 47;
    private static double[] _surfStats = new double[BINS];
    private const double WALL_STICK = 160;

    // State
    private Point _myLocation;
    private Point _enemyLocation;
    private Point? _lastGoToPoint;
    private double _direction = 1;
    private double _oppEnergy = 100.0;

    // Collections
    private List<EnemyWave> _enemyWaves = new List<EnemyWave>();
    private List<int> _surfDirections = new List<int>();
    private List<double> _surfAbsBearings = new List<double>();

    // Entry point
    static void Main(string[] args)
    {
        new SurferMovement().Start();
    }

    public override void Run()
    {
        _enemyWaves.Clear();
        _surfDirections.Clear();
        _surfAbsBearings.Clear();

        // Independent movement
        AdjustGunForBodyTurn = true;
        AdjustRadarForGunTurn = true;
        AdjustRadarForBodyTurn = true;

        // Visuals
        BodyColor = Color.Cyan;
        GunColor = Color.Blue;
        RadarColor = Color.White;

        // Infinite Radar Lock
        while (IsRunning)
        {
            TurnRadarLeft(double.PositiveInfinity);
            Go();
        }
    }

    public override void OnScannedBot(ScannedBotEvent e)
    {
        _myLocation = new Point(X, Y);

        // Calculate absolute bearing to enemy (0 = East, CCW)
        double absBearing = AbsoluteBearing(_myLocation, new Point(e.X, e.Y));

        // Lateral Velocity Calculation
        // Measures how fast we are moving perpendicular to the enemy
        // Speed is +/-, (e.Direction - absBearing) is the relative angle
        double lateralVelocity = Speed * Math.Sin((Direction - e.Direction) * (Math.PI / 180.0));

        // Radar Lock
        double radarTurn = Utils.NormalRelativeAngle(absBearing - (RadarDirection * Math.PI / 180.0));
        SetTurnRadarLeft(radarTurn * (180.0 / Math.PI) * 2);

        // Store Surfing Data
        _surfDirections.Insert(0, lateralVelocity >= 0 ? 1 : -1);
        _surfAbsBearings.Insert(0, absBearing + Math.PI); // +PI to point away from enemy

        // Detect Bullet Fire
        double bulletPower = _oppEnergy - e.Energy;
        if (bulletPower < 3.01 && bulletPower > 0.09 && _surfDirections.Count > 2)
        {
            EnemyWave ew = new EnemyWave();
            ew.FireTime = TurnNumber - 1;
            ew.BulletVelocity = BulletVelocity(bulletPower);
            ew.DistanceTraveled = BulletVelocity(bulletPower);
            ew.Direction = _surfDirections[2];
            ew.DirectAngle = _surfAbsBearings[2];
            ew.FireLocation = _enemyLocation;
            _enemyWaves.Add(ew);
        }

        _oppEnergy = e.Energy;
        _enemyLocation = new Point(e.X, e.Y);

        UpdateWaves();
        DoSurfing();
    }

    public override void OnHitByBullet(HitByBulletEvent e)
    {
        if (_enemyWaves.Count > 0)
        {
            Point hitBulletLocation = new Point(X, Y); // Approximate hit location as current loc
            EnemyWave hitWave = null;

            // Find the wave that likely hit us
            foreach (var ew in _enemyWaves)
            {
                double dist = Distance(_myLocation, ew.FireLocation);
                double velocityDiff = Math.Abs(BulletVelocity(e.Bullet.Power) - ew.BulletVelocity);

                if (Math.Abs(ew.DistanceTraveled - dist) < 50 && velocityDiff < 0.001)
                {
                    hitWave = ew;
                    break;
                }
            }

            if (hitWave != null)
            {
                LogHit(hitWave, hitBulletLocation);
                _enemyWaves.Remove(hitWave);
            }
        }
    }

    public override void OnTick(TickEvent e)
    {
        // Draw waves for debugging
        if (IsDebuggingEnabled)
        {
            Graphics.SetStrokeColor(Color.Red);
            foreach (var w in _enemyWaves)
            {
                // Draw circle representing the wave
                double radius = w.DistanceTraveled;
                Graphics.DrawCircle(w.FireLocation.X, w.FireLocation.Y, 2);
                Graphics.DrawCircle(w.FireLocation.X, w.FireLocation.Y, radius);
            }
        }
    }

    private void UpdateWaves()
    {
        for (int i = 0; i < _enemyWaves.Count; i++)
        {
            EnemyWave ew = _enemyWaves[i];
            ew.DistanceTraveled = (TurnNumber - ew.FireTime) * ew.BulletVelocity;

            double distToSource = Distance(_myLocation, ew.FireLocation);

            if (ew.DistanceTraveled > distToSource + 50)
            {
                _enemyWaves.RemoveAt(i);
                i--;
            }
        }
    }

    private EnemyWave GetClosestSurfableWave()
    {
        double closestDistance = 50000;
        EnemyWave surfWave = null;

        foreach (var ew in _enemyWaves)
        {
            double dist = Distance(_myLocation, ew.FireLocation) - ew.DistanceTraveled;

            if (dist > ew.BulletVelocity && dist < closestDistance)
            {
                surfWave = ew;
                closestDistance = dist;
            }
        }
        return surfWave;
    }

    private static int GetFactorIndex(EnemyWave ew, Point targetLocation)
    {
        double offsetAngle = Utils.NormalRelativeAngle(AbsoluteBearing(ew.FireLocation, targetLocation) - ew.DirectAngle);
        double factor = offsetAngle / MaxEscapeAngle(ew.BulletVelocity) * ew.Direction;

        return (int)Limit(0, (factor * ((BINS - 1) / 2)) + ((BINS - 1) / 2), BINS - 1);
    }

    private void LogHit(EnemyWave ew, Point targetLocation)
    {
        int index = GetFactorIndex(ew, targetLocation);

        for (int x = 0; x < BINS; x++)
        {
            _surfStats[x] += 1.0 / (Math.Pow(index - x, 2) + 1);
        }
    }

    // Predicts future positions to find the safest spot
    private List<Point> PredictPositions(EnemyWave surfWave, int direction)
    {
        Point predictedPosition = new Point(_myLocation.X, _myLocation.Y);
        double predictedVelocity = Speed;
        double predictedHeading = Direction * (Math.PI / 180.0);
        double maxTurning, moveAngle, moveDir;
        List<Point> traveledPoints = new List<Point>();

        int counter = 0;
        bool intercepted = false;

        do
        {
            double distance = Distance(predictedPosition, surfWave.FireLocation);
            double offset = Math.PI / 2 - 1 + distance / 400;

            double absBearing = AbsoluteBearing(surfWave.FireLocation, predictedPosition);

            // Calculate Wall Smoothing target
            // Angle is Absolute Bearing + (Direction * Offset)
            // Note: 0=East, CCW
            double targetAngle = absBearing + (direction * offset);

            moveAngle = WallSmoothing(predictedPosition, targetAngle, direction) - predictedHeading;
            moveDir = 1;

            if (Math.Cos(moveAngle) < 0)
            {
                moveAngle += Math.PI;
                moveDir = -1;
            }

            moveAngle = Utils.NormalRelativeAngle(moveAngle);

            // Tank Royale Max Turn Rate: 10 - 0.75 * abs(speed) (Degrees)
            // Convert to Radians: (10 - 0.75 * abs(v)) * PI / 180
            double maxTurnDegrees = 10.0 - 0.75 * Math.Abs(predictedVelocity);
            maxTurning = maxTurnDegrees * (Math.PI / 180.0);

            predictedHeading = Utils.NormalRelativeAngle(predictedHeading + Limit(-maxTurning, moveAngle, maxTurning));

            // Acceleration Logic (Accel=1, Decel=-2)
            if (predictedVelocity * moveDir < 0)
            {
                // Braking / reversing direction
                predictedVelocity += (2 * moveDir);
            }
            else
            {
                // Accelerating
                predictedVelocity += moveDir;
            }
            predictedVelocity = Limit(-8, predictedVelocity, 8);

            // Calculate new position
            predictedPosition = Project(predictedPosition, predictedHeading, predictedVelocity);
            traveledPoints.Add(predictedPosition);

            counter++;

            // Check if wave intercepts this point
            double waveDistNow = surfWave.DistanceTraveled + (counter * surfWave.BulletVelocity);
            if (Distance(predictedPosition, surfWave.FireLocation) - 20 < waveDistNow)
            {
                intercepted = true;
            }

        } while (!intercepted && counter < 500);

        if (traveledPoints.Count > 1)
            traveledPoints.RemoveAt(traveledPoints.Count - 1);

        return traveledPoints;
    }

    private double CheckDanger(EnemyWave surfWave, Point position)
    {
        int index = GetFactorIndex(surfWave, position);
        double distance = Distance(position, surfWave.FireLocation);
        return _surfStats[index] / distance;
    }

    private Point? GetBestPoint(EnemyWave surfWave)
    {
        if (surfWave.SafePoints == null)
        {
            List<Point> forwardPoints = PredictPositions(surfWave, 1);
            List<Point> reversePoints = PredictPositions(surfWave, -1);

            double fMinDanger = double.PositiveInfinity;
            double rMinDanger = double.PositiveInfinity;
            int fMinIndex = 0;
            int rMinIndex = 0;

            for (int i = 0; i < forwardPoints.Count; i++)
            {
                double danger = CheckDanger(surfWave, forwardPoints[i]);
                if (danger <= fMinDanger) { fMinDanger = danger; fMinIndex = i; }
            }

            for (int i = 0; i < reversePoints.Count; i++)
            {
                double danger = CheckDanger(surfWave, reversePoints[i]);
                if (danger <= rMinDanger) { rMinDanger = danger; rMinIndex = i; }
            }

            List<Point> bestPoints;
            int minDangerIndex;

            if (fMinDanger < rMinDanger)
            {
                bestPoints = forwardPoints;
                minDangerIndex = fMinIndex;
            }
            else
            {
                bestPoints = reversePoints;
                minDangerIndex = rMinIndex;
            }

            Point bestPoint = bestPoints[minDangerIndex];

            // Trim list to just the path to best point
            if (minDangerIndex < bestPoints.Count)
            {
                bestPoints = bestPoints.Take(minDangerIndex + 1).ToList();
            }

            surfWave.SafePoints = bestPoints;
            // Add current location to beginning
            surfWave.SafePoints.Insert(0, _myLocation);
        }
        else if (surfWave.SafePoints.Count > 1)
        {
            surfWave.SafePoints.RemoveAt(0);
        }

        if (surfWave.SafePoints.Count >= 1)
        {
            for (int i = 0; i < surfWave.SafePoints.Count; i++)
            {
                Point p = surfWave.SafePoints[i];
                // DistanceSq > 400 (20*20)
                if (DistanceSq(p, _myLocation) > 400 * 1.1)
                {
                    return p;
                }
            }
            return surfWave.SafePoints[surfWave.SafePoints.Count - 1];
        }

        return null; // Should be handled by caller
    }

    private void DoSurfing()
    {
        EnemyWave surfWave = GetClosestSurfableWave();
        double distToEnemy = Distance(_enemyLocation, _myLocation);

        if (surfWave == null || distToEnemy < 50)
        {
            // Standard orbiting / "Away" movement
            double absBearing = AbsoluteBearing(_myLocation, _enemyLocation);
            double headingRadians = Direction * (Math.PI / 180.0);
            double stick = 160;
            double v2;
            double offset = Math.PI / 2 + 1 - distToEnemy / 400;

            // Simple wall smoothing for non-surfing movement
            // Iterate until we find a point inside the arena
            // Using a simple loop decrementing offset
            while (!IsPointInArena(Project(_myLocation, v2 = absBearing + _direction * (offset -= 0.02), stick))) ;

            if (offset < Math.PI / 3)
                _direction = -_direction;

            SetForward(50 * Math.Cos(v2 - headingRadians));
            // SetTurnLeft takes degrees
            SetTurnLeft(Math.Atan2(Math.Sin(v2 - headingRadians), Math.Cos(v2 - headingRadians)) * (180.0 / Math.PI));
        }
        else
        {
            Point? bestPoint = GetBestPoint(surfWave);
            if (bestPoint != null)
                GoTo(bestPoint);
        }
    }

    private void GoTo(Point? destination)
    {
        if (destination == null)
        {
            if (_lastGoToPoint != null) destination = _lastGoToPoint;
            else return;
        }

        _lastGoToPoint = destination;

        // Calculate angle to destination
        double angleToTarget = AbsoluteBearing(_myLocation, destination ?? new());
        double currentHeading = Direction * (Math.PI / 180.0);

        double relativeAngle = Utils.NormalRelativeAngle(angleToTarget - currentHeading);

        double distance = Distance(_myLocation, destination ?? new Point());

        // Back-As-Forward Logic
        if (Math.Abs(relativeAngle) > Math.PI / 2)
        {
            distance = -distance;
            if (relativeAngle > 0) relativeAngle -= Math.PI;
            else relativeAngle += Math.PI;
        }

        // Convert radians to degrees for API
        SetTurnLeft(relativeAngle * (180.0 / Math.PI));
        SetForward(distance);
    }

    // Iterative WallSmoothing (Kawigi) adapted for 0=East
    private double WallSmoothing(Point botLocation, double angle, int orientation)
    {
        while (!IsPointInArena(Project(botLocation, angle, WALL_STICK)))
        {
            angle += orientation * 0.05;
        }
        return angle;
    }

    private bool IsPointInArena(Point p)
    {
        return p.X > 18 && p.X < ArenaWidth - 18 && p.Y > 18 && p.Y < ArenaHeight - 18;
    }

    // Helper Math
    private static Point Project(Point source, double angle, double length)
    {
        // 0 = East (Cos for X, Sin for Y)
        return new Point(source.X + Math.Cos(angle) * length, source.Y + Math.Sin(angle) * length);
    }

    private static double AbsoluteBearing(Point source, Point target)
    {
        // Atan2(y, x) is standard 0=East
        return Math.Atan2(target.Y - source.Y, target.X - source.X);
    }

    private static double Distance(Point p1, Point p2)
    {
        return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
    }

    private static double DistanceSq(Point p1, Point p2)
    {
        return Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2);
    }

    private static double Limit(double min, double value, double max)
    {
        return Math.Max(min, Math.Min(value, max));
    }

    private static double BulletVelocity(double power)
    {
        return 20.0 - (3.0 * power);
    }

    private static double MaxEscapeAngle(double velocity)
    {
        return Math.Asin(8.0 / velocity);
    }

    // Wave Class
    private class EnemyWave
    {
        public Point FireLocation { get; set; }
        public long FireTime { get; set; }
        public double BulletVelocity { get; set; }
        public double DirectAngle { get; set; }
        public double DistanceTraveled { get; set; }
        public int Direction { get; set; }
        public List<Point> SafePoints { get; set; }
    }

    // Utility class for Angle Normalization
    private static class Utils
    {
        public static double NormalRelativeAngle(double angle)
        {
            return Math.Atan2(Math.Sin(angle), Math.Cos(angle));
        }
    }
}