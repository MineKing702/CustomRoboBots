using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;
using System;

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

        radarTurn += Math.Sign(radarTurn) * 5; // 5° overshoot
        SetTurnRadarLeft(radarTurn);
    }
}
