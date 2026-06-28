# Ranked Leaderboard And Rating Spec

## Purpose

This document is the current source of truth for the ranked leaderboard, Elo flow, and rank thresholds.

This spec currently applies to:

- Ranked survival matchmaking
- Ranked creative matchmaking
- Future ranked server implementation

Singleplayer remains separate from ranked Elo and can later have a time-based leaderboard only.

## Base Elo Results

These values apply before any run-time bonus is added.

| Match Result | Elo Change |
| --- | ---: |
| Win | +15 |
| Loss | -15 |
| Win by Forfeit | +10 |
| Loss by Forfeit | -20 |

## Survival Bonus By Finish Time

These bonuses are added on top of the base Elo result for a ranked survival win.

| Finish Time | Bonus Elo |
| --- | ---: |
| Sub 23 | +6 |
| 23:xx | +5 |
| 24:xx | +4 |
| 25:xx | +4 |
| 26:xx | +3 |
| 27:xx | +3 |
| 28:xx | +2 |
| 29:xx | +2 |
| 30:xx | +1 |
| 31:xx | +1 |
| 32:xx | +1 |
| 33:00+ | +0 |

## Creative Bonus By Finish Time

These bonuses are added on top of the base Elo result for a ranked creative win.

| Finish Time | Bonus Elo |
| --- | ---: |
| 4:54 or faster | +6 |
| 4:55 | +5 |
| 4:56 | +5 |
| 4:57 | +5 |
| 4:58 | +4 |
| 4:59 | +4 |
| 5:00 | +4 |
| 5:01 to 5:05 | +3 |
| 5:06 to 5:10 | +2 |
| 5:11 to 5:15 | +1 |
| 5:16+ | +0 |

## Current Rank Thresholds

### Copper

| Rank | Elo Range |
| --- | --- |
| Copper 1 | 0-150 |
| Copper 2 | 151-300 |
| Copper 3 | 301-499 |

### Silver

| Rank | Elo Range |
| --- | --- |
| Silver 1 | 500-650 |
| Silver 2 | 651-800 |
| Silver 3 | 801-999 |

### Gold

| Rank | Elo Range |
| --- | --- |
| Gold 1 | 1000-1150 |
| Gold 2 | 1151-1299 |
| Gold 3 | 1300-1499 |

### Lithium

| Rank | Elo Range |
| --- | --- |
| Lithium 1 | 1500-1650 |
| Lithium 2 | 1651-1799 |
| Lithium 3 | 1800-1999 |

### Kyanite

| Rank | Elo Range |
| --- | --- |
| Kyanite | 2000+ |

## Rank Presentation

- Ranks should visually use the exact in-game ore/resource icons where possible.
- Rank colors should follow the same visual language as the in-game resources.
- The leaderboard UI panel on the main menu is currently a placeholder for this feature.

## Matchmaking Rules

### Current

- Match against anyone, regardless of rank.

### Planned

- Match within one full rank tier up or down.
- Example:
  - A Silver player can match with Silver, Copper, or Gold.

## Planned Leaderboard Views

### Ranked Leaderboard

- Highest Elo
- Overall rank position
- Win count
- Loss count
- Forfeit count

### Time Leaderboard

- Fastest match time
- Average match time
- Slowest match time

## Notes

- The current ranked leaderboard values are only for ranked play.
- Singleplayer can later expose a separate fastest-time leaderboard without using Elo.
- Survival and creative use different bonus tables, but the same base win/loss/forfeit rules.
