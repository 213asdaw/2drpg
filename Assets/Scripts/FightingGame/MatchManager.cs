using System;
using UnityEngine;

namespace FightingGame
{
    public sealed class MatchManager
    {
        private float roundTimer;
        private float phaseTimer;
        private int playerOneRoundWins;
        private int playerTwoRoundWins;
        private int currentRound = 1;
        private FighterController lastRoundWinner;
        private FighterController playerOneRef;
        private FighterController playerTwoRef;

        public MatchPhase Phase { get; private set; } = MatchPhase.Intro;
        public float RoundTimer => roundTimer;
        public int CurrentRound => currentRound;
        public int PlayerOneRoundWins => playerOneRoundWins;
        public int PlayerTwoRoundWins => playerTwoRoundWins;
        public FighterController LastRoundWinner => lastRoundWinner;
        public string StatusMessage { get; private set; } = "READY";

        public event Action RoundStarted;
        public event Action<FighterController> RoundEnded;
        public event Action<FighterController> MatchEnded;

        public void BeginMatch(FighterController playerOne, FighterController playerTwo)
        {
            playerOneRef = playerOne;
            playerTwoRef = playerTwo;
            playerOneRoundWins = 0;
            playerTwoRoundWins = 0;
            currentRound = 1;
            lastRoundWinner = null;
            BeginIntro("ROUND 1 — FIGHT!");
        }

        public void Tick(float deltaTime)
        {
            switch (Phase)
            {
                case MatchPhase.Intro:
                    phaseTimer -= deltaTime;
                    if (phaseTimer <= 0f)
                    {
                        StartFighting();
                    }

                    break;

                case MatchPhase.Fighting:
                    roundTimer -= deltaTime;
                    if (!playerOneRef.IsAlive || !playerTwoRef.IsAlive)
                    {
                        FighterController winner = playerOneRef.IsAlive ? playerOneRef : playerTwoRef;
                        FinishRound(winner, winner.DisplayName + " KO!");
                    }
                    else if (roundTimer <= 0f)
                    {
                        if (Mathf.Approximately(playerOneRef.Health, playerTwoRef.Health))
                        {
                            FinishRound(null, "TIME — DRAW");
                        }
                        else
                        {
                            FighterController winner = playerOneRef.Health > playerTwoRef.Health ? playerOneRef : playerTwoRef;
                            FinishRound(winner, "TIME — " + winner.DisplayName + " WINS");
                        }
                    }

                    break;

                case MatchPhase.RoundEnd:
                    phaseTimer -= deltaTime;
                    if (phaseTimer <= 0f)
                    {
                        if (playerOneRoundWins >= FightConstants.RoundsToWin || playerTwoRoundWins >= FightConstants.RoundsToWin)
                        {
                            BeginMatchEnd(lastRoundWinner);
                        }
                        else
                        {
                            currentRound++;
                            BeginIntro("ROUND " + currentRound + " — FIGHT!");
                        }
                    }

                    break;

                case MatchPhase.MatchEnd:
                    phaseTimer -= deltaTime;
                    break;
            }
        }

        public bool ControlsEnabled =>
            Phase == MatchPhase.Intro || Phase == MatchPhase.Fighting;

        public void RestartMatch()
        {
            BeginMatch(playerOneRef, playerTwoRef);
        }

        private void BeginIntro(string message)
        {
            Phase = MatchPhase.Intro;
            phaseTimer = 0.15f;
            roundTimer = FightConstants.RoundDuration;
            StatusMessage = message;
        }

        private void StartFighting()
        {
            Phase = MatchPhase.Fighting;
            StatusMessage = "FIGHT!";
            RoundStarted?.Invoke();
        }

        private void FinishRound(FighterController winner, string message)
        {
            Phase = MatchPhase.RoundEnd;
            phaseTimer = FightConstants.RoundEndDelay;
            lastRoundWinner = winner;
            StatusMessage = message;

            if (winner == playerOneRef)
            {
                playerOneRoundWins++;
            }
            else if (winner == playerTwoRef)
            {
                playerTwoRoundWins++;
            }

            RoundEnded?.Invoke(winner);
        }

        private void BeginMatchEnd(FighterController winner)
        {
            Phase = MatchPhase.MatchEnd;
            phaseTimer = FightConstants.MatchEndDelay;
            StatusMessage = winner != null
                ? winner.DisplayName + " MATCH WIN!"
                : "MATCH DRAW";
            MatchEnded?.Invoke(winner);
        }
    }
}
