using System.Collections.Generic;

public class BowlingScorer
{
    private List<int> rolls = new List<int>();

    public void AddRoll(int pins)
    {
        rolls.Add(pins);
    }

    public int GetTotalScore()
    {
        int score = 0;
        int index = 0;

        for (int frame = 0; frame < 10 && index < rolls.Count; frame++)
        {
            if (rolls[index] == 10) // Strike
            {
                score += 10 + GetRoll(index + 1) + GetRoll(index + 2);
                index += 1;
            }
            else if (GetRoll(index) + GetRoll(index + 1) == 10) // Spare
            {
                score += 10 + GetRoll(index + 2);
                index += 2;
            }
            else
            {
                score += GetRoll(index) + GetRoll(index + 1);
                index += 2;
            }
        }

        return score;
    }

    int GetRoll(int i)
    {
        return i < rolls.Count ? rolls[i] : 0;
    }
}
