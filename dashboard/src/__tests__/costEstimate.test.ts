import fc from 'fast-check';
import { describe, it, expect } from 'vitest';
import { calculateCost } from '../pages/AgentsPage';

describe('Cost Estimate Calculation', () => {
  it('should equal (tokens / 1000) * rate rounded to 2 decimal places', () => {
    fc.assert(
      fc.property(
        fc.nat({ max: 1_000_000 }),
        fc.float({ min: 0, max: 100, noNaN: true }),
        (tokens, rate) => {
          const result = calculateCost(tokens, rate);
          const expected = Math.round((tokens / 1000) * rate * 100) / 100;
          expect(result).toBe(expected);
        }
      )
    );
  });

  it('should return 0 when tokens is 0', () => {
    fc.assert(
      fc.property(
        fc.float({ min: 0, max: 100, noNaN: true }),
        (rate) => {
          expect(calculateCost(0, rate)).toBe(0);
        }
      )
    );
  });

  it('should return 0 when rate is 0', () => {
    fc.assert(
      fc.property(
        fc.nat({ max: 1_000_000 }),
        (tokens) => {
          expect(calculateCost(tokens, 0)).toBe(0);
        }
      )
    );
  });

  it('should always produce a non-negative result', () => {
    fc.assert(
      fc.property(
        fc.nat({ max: 1_000_000 }),
        fc.float({ min: 0, max: 100, noNaN: true }),
        (tokens, rate) => {
          expect(calculateCost(tokens, rate)).toBeGreaterThanOrEqual(0);
        }
      )
    );
  });
});
