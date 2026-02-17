using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;
using System;

// ------------------------------------------------------------------
// MyFirstBot
// ------------------------------------------------------------------
// A sample bot originally made for Robocode by Mathew Nelson.
//
// Probably the first bot you will learn about.
// Moves in a seesaw motion and spins the gun around at each end.
// ------------------------------------------------------------------
public class RadarLock : Bot
{
    // The main method starts our bot
    static void Main(string[] args)
    {
        new RadarLock().Start();
    }

    // Called when a new round is started -> initialize and do some movement
    public override void Run()
    {

        AdjustGunForBodyTurn = true;
        AdjustRadarForBodyTurn = true;
        AdjustRadarForGunTurn = true;


        // Repeat while the bot is running
        while (IsRunning)
        {
            TurnRadarRight(double.PositiveInfinity);


            Go();
            
        }
    }

    // We saw another bot -> fire!
    public override void OnScannedBot(ScannedBotEvent evt)
    {
        
        double radarTurn = RadarBearingTo(evt.X, evt.Y);
        Console.WriteLine($"I'm at x: {X}, y: {Y}; Scanned enemy bot at x: {evt.X}, y: {evt.Y}; Radar Direction: {RadarDirection}; Radar Turn: {radarTurn}");

        radarTurn += Math.Sign(radarTurn) * 5; // 2° overshoot
        SetTurnRadarLeft(radarTurn);
        
        
        
        /*
        double angleDifference = Direction + BearingTo(evt.X, evt.Y);
        double turningPoint = NormalizeRelativeAngle(angleDifference - RadarDirection);
        double tolerance = Math.Min(Math.Atan(36.0 / DistanceTo(evt.X, evt.Y)), 45);
        turningPoint += (turningPoint < 0 ? -tolerance : tolerance);
        SetTurnRadarLeft(turningPoint);
        */
        
       // double enemyAngle = DirectionTo(evt.X, evt.Y); // Absolute angle to enemy
       // double turn = NormalizeRelativeAngle(enemyAngle - RadarDirection); // Radar turn
       /*
        double turn = RadarBearingTo(evt.X, evt.Y); 
        double distance = DistanceTo(evt.X, evt.Y);
        double tolerance = Math.Min(Math.Atan(36.0 / distance) * (180.0 / Math.PI), 45.0); // Convert to degrees
        turn += (turn < 0 ? -tolerance : tolerance);
        SetTurnRadarLeft(turn);
       */
        //Console.WriteLine($"Tolerance: {tolerance}");
    }

    // We were hit by a bullet -> turn perpendicular to the bullet
    public override void OnHitByBullet(HitByBulletEvent evt)
    {
        
    }
}
