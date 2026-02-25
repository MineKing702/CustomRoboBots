using System;
using System.Collections.Generic;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

// ------------------------------------------------------------------
// RadarLock + Play-It-Forward Targeting (Single File Version)
// ------------------------------------------------------------------
public class PredictiveShooter : Bot
{
    private const int NGRAM_ORDER = 4;
    private const double GUN_FACTOR = 5;
    private const double MIN_ENERGY = 12;

    private Dictionary<int, EnemyData> enemyData = new();

    static void Main(string[] args)
    {
        new PredictiveShooter().Start();
    }

    public override void Run()
    {
        AdjustGunForBodyTurn = true;
        AdjustRadarForBodyTurn = true;
        AdjustRadarForGunTurn = true;

        while (IsRunning)
        {
            TurnRadarRight(double.PositiveInfinity);
            Go();
        }
    }

    public override void OnScannedBot(ScannedBotEvent e)
    {
        // -----------------------
        // RADAR LOCK (your logic)
        // -----------------------
        double radarTurn = RadarBearingTo(e.X, e.Y);
        radarTurn += Math.Sign(radarTurn) * 5; // overshoot
        SetTurnRadarLeft(radarTurn);

        // -----------------------
        // Enemy Data Setup
        // -----------------------
        if (!enemyData.ContainsKey(e.ScannedBotId))
            enemyData[e.ScannedBotId] = new EnemyData();

        EnemyData data = enemyData[e.ScannedBotId];

        double distance = DistanceTo(e.X, e.Y);

        // -----------------------
        // Firepower Calculation
        // -----------------------
        double firePower = Energy / distance * GUN_FACTOR;
        firePower = Math.Min(3, Math.Max(0.1, firePower));

        if (Energy < MIN_ENERGY)
            firePower = 1;

        double bulletSpeed = CalcBulletSpeed(firePower);

        // -----------------------
        // State Tracking
        // -----------------------
        double currentDirection = e.Direction * Math.PI / 180.0;
        double currentSpeed = e.Speed;

        double acceleration = data.HasPrevious ? currentSpeed - data.LastSpeed : 0;
        double angularVelocity = data.HasPrevious
            ? (currentDirection - data.LastDirection + Math.PI) % (2 * Math.PI) - Math.PI
            : 0;

        State currentState = new State(angularVelocity, currentSpeed, acceleration);

        data.LastSpeed = currentSpeed;
        data.LastDirection = currentDirection;
        data.HasPrevious = true;

        data.StateHistory.Add(currentState);

        if (data.StateHistory.Count >= NGRAM_ORDER)
        {
            List<State> contextStates =
                data.StateHistory.GetRange(data.StateHistory.Count - (NGRAM_ORDER - 1), NGRAM_ORDER - 1);

            StateSequence contextKey = new StateSequence(contextStates);

            if (!data.NgramTree.ContainsKey(contextKey))
                data.NgramTree[contextKey] = new TransitionSegmentTree();

            data.NgramTree[contextKey].Add(currentState);
        }

        // -----------------------
        // Play-It-Forward Prediction
        // -----------------------
        double predictedX = e.X;
        double predictedY = e.Y;
        double predictedDirection = currentDirection;
        double predictedSpeed = currentSpeed;

        int time = 0;

        List<State> simContext = null;

        if (data.StateHistory.Count >= NGRAM_ORDER - 1)
        {
            simContext = new List<State>(
                data.StateHistory.GetRange(data.StateHistory.Count - (NGRAM_ORDER - 1), NGRAM_ORDER - 1));
        }

        while (time * bulletSpeed < DistanceTo(predictedX, predictedY) && time < 100)
        {
            if (simContext != null)
            {
                StateSequence simKey = new StateSequence(simContext);

                if (data.NgramTree.ContainsKey(simKey))
                {
                    State next = data.NgramTree[simKey].GetMostFrequent();
                    predictedDirection += next.AngularVelocity / 1024.0;
                    predictedSpeed += next.Acceleration;

                    simContext.RemoveAt(0);
                    simContext.Add(next);
                }
            }

            predictedX += predictedSpeed * Math.Cos(predictedDirection);
            predictedY += predictedSpeed * Math.Sin(predictedDirection);

            time++;
        }

        // Clamp prediction to arena
        predictedX = Math.Max(18, Math.Min(ArenaWidth - 18, predictedX));
        predictedY = Math.Max(18, Math.Min(ArenaHeight - 18, predictedY));

        // -----------------------
        // Gun Turn
        // -----------------------
        double gunTurn = GunBearingTo(predictedX, predictedY);
        SetTurnGunLeft(gunTurn);

        // -----------------------
        // Fire
        // -----------------------
        if (Math.Abs(GunTurnRemaining) < 1 && GunHeat == 0)
        {
            SetFire(firePower);
        }
    }
}

// ------------------------------------------------------------------
// Supporting Classes (same file)
// ------------------------------------------------------------------

public struct State
{
    public int AngularVelocity;
    public int Speed;
    public int Acceleration;

    public State(double angularVelocity, double speed, double acceleration)
    {
        AngularVelocity = (int)(angularVelocity * 1024);
        Speed = (int)Math.Round(speed);

        if (acceleration > 0.1) Acceleration = 1;
        else if (acceleration < -0.1) Acceleration = -1;
        else Acceleration = 0;
    }
}

public class StateSequence
{
    public List<State> States;

    public StateSequence(IEnumerable<State> states)
    {
        States = new List<State>(states);
    }

    public override bool Equals(object obj)
    {
        if (obj is not StateSequence other || States.Count != other.States.Count)
            return false;

        for (int i = 0; i < States.Count; i++)
            if (!States[i].Equals(other.States[i]))
                return false;

        return true;
    }

    public override int GetHashCode()
    {
        int hash = 17;
        foreach (var s in States)
            hash = hash * 31 + s.GetHashCode();
        return hash;
    }
}

public class EnemyData
{
    public List<State> StateHistory = new();
    public Dictionary<StateSequence, TransitionSegmentTree> NgramTree = new();
    public bool HasPrevious = false;
    public double LastDirection;
    public double LastSpeed;
}

public class TransitionSegmentTree
{
    private Dictionary<State, int> data = new();

    public void Add(State s)
    {
        if (data.ContainsKey(s))
            data[s]++;
        else
            data[s] = 1;
    }

    public State GetMostFrequent()
    {
        State best = default;
        int max = -1;

        foreach (var kvp in data)
        {
            if (kvp.Value > max)
            {
                max = kvp.Value;
                best = kvp.Key;
            }
        }

        return best;
    }
}