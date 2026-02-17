using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;
using Robocode.TankRoyale.BotApi.Graphics;
using System;
using System.Numerics;
public class CornerMovement : Bot
{

    double arenaHeight;
    double arenaWidth;

    int enemyQuadrant;

    Vector2 lastTargetPos = Vector2.Zero;
    bool atTarget = false;

    // our bots current position
    Vector2 position;
    Vector2 targetPos;

    GameStartedEvent StartEvt;

    static void Main(string[] args)
    {
        new CornerMovement().Start();
    }
    public override void Run()
    {
        arenaHeight = StartEvt.GameSetup.ArenaHeight;
        arenaWidth = StartEvt.GameSetup.ArenaWidth;

        // Start infinite radar sweep
        SetTurnRadarRight(double.PositiveInfinity);

        // Main loop
        while (IsRunning)
        {
            // Update current position each tick
            position = new Vector2((float)X, (float)Y);

            // If we have a target, move toward it
            if (targetPos != Vector2.Zero)
            {
                MoveToTarget(targetPos);
            }

            // Go() executes all movement/turn commands for this tick
            Go();
        }
    }
    public override void OnGameStarted(GameStartedEvent evt)
    {
        StartEvt = evt;
    }

    public override void OnRoundStarted(RoundStartedEvent evt)
    {
        // Reset movement state
        targetPos = Vector2.Zero;
        atTarget = false;

        // Restart infinite radar sweep
        SetTurnRadarRight(double.PositiveInfinity);
    }
    public override void OnScannedBot(ScannedBotEvent evt)
    {
        int oldQuadrant = enemyQuadrant;
        enemyQuadrant = QuadrantNum(evt.X, evt.Y);

        if (oldQuadrant != enemyQuadrant)
        {
            Console.WriteLine($"Scanned bot in quadrant {enemyQuadrant}");
        }

        int targetQuadrant = GetOppositeQuadrant();

        Vector2 newTarget = GetQuadrantCenter(targetQuadrant);

        // Only update if the target has changed
        if (targetPos != newTarget)
        {
            targetPos = newTarget;
            atTarget = false; // new target, so we’re no longer “atTarget”
            Graphics.DrawCircle(targetPos.X, targetPos.Y, 50f);
            Graphics.ToSvg();
            Console.WriteLine($"target x: {targetPos.X} and target y: {targetPos.Y}");
        }
    }

    void MoveToTarget(Vector2 target)
    {
        Vector2 toTarget = target - position;
        double distance = toTarget.Length();

        // ----- 1. Stop if already there -----
        if (distance < 1.0)
        {
            SetForward(0);
            SetTurnRight(0);
            atTarget = true;
            return;
        }

        atTarget = false;

        // ----- 2. Calculate angle to target -----
        double angleToTarget = Math.Atan2(toTarget.Y, toTarget.X) * (180 / Math.PI);
        double turnAngle = NormalizeBearing(angleToTarget - Direction);

        // ----- 3. If we are NOT facing target, stop and turn only -----
        if (Math.Abs(turnAngle) > 2) // tolerance so we don't jitter
        {
            SetStop(); // ensure no movement while turning

            if (turnAngle < 0)
            {
                SetTurnRight(turnAngle);
                Console.WriteLine($"Turn Angle Right {turnAngle}");
            }
            else
            {
                Console.WriteLine($"Turn Angle Left {-turnAngle}");
                SetTurnLeft(turnAngle);
            }

            return; // IMPORTANT: don't move this tick
        }

        // ----- 4. Now we are facing target => move straight -----
        SetTurnRight(0); // ensure no residual turning
        SetForward(distance);
    }

    // Normalize an angle to -180..180 degrees
    double NormalizeBearing(double angle)
    {
        while (angle > 180) angle -= 360;
        while (angle < -180) angle += 360;
        return angle;
    }

    int QuadrantNum(double x, double y)
    {
        // ----- Center box bounds (1/4 arena area) -----
        double centerMinX = arenaWidth / 4.0;
        double centerMaxX = arenaWidth * 3.0 / 4.0;

        double centerMinY = arenaHeight / 4.0;
        double centerMaxY = arenaHeight * 3.0 / 4.0;

        // ----- Check middle rectangle first -----
        if (x >= centerMinX && x <= centerMaxX &&
            y >= centerMinY && y <= centerMaxY)
        {
            return 5; // middle quadrant
        }

        // ----- Normal 4-quadrant logic -----
        int quadrant = (y >= arenaHeight / 2.0) ? 1 : 3;

        if (x >= arenaWidth / 2.0)
            quadrant++;

        return quadrant;
    }

    int GetOppositeQuadrant()
    {

        int oppQuad = 0;
        if (enemyQuadrant != 5)
        {
            oppQuad = 5 - enemyQuadrant;
        }

        return oppQuad;
    }

    Vector2 GetQuadrantCenter(int q)
    {
        if (q < 1 || q > 4)
            throw new ArgumentOutOfRangeException(nameof(q), "Quadrant must be 1–4.");

        double x = ((q - 1) % 2 == 0) ? arenaWidth * 0.25 : arenaWidth * 0.75;
        double y = (q <= 2) ? arenaHeight * 0.75 : arenaHeight * 0.25;

        return new Vector2((float)x, (float)y);
    }
}
