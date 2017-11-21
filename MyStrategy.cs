using Com.CodeGame.CodeWars2017.DevKit.CSharpCgdk.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Com.CodeGame.CodeWars2017.DevKit.CSharpCgdk
{
    public sealed class MyStrategy : IStrategy
    {
        private RandomLong random;
        private Random r;

        private TerrainType[][] terrainTypeByCellXY;
        private WeatherType[][] weatherTypeByCellXY;

        private Player me;
        private World world;
        private Game game;
        private Move move;

        delegate void AnotherMove(Move move);

        private Queue<AnotherMove> delayedMoves = new Queue<AnotherMove>();
        private Dictionary<long, int> updateTickByVehicleId = new Dictionary<long, int>();
        private Dictionary<long, Vehicle> vehicleById = new Dictionary<long, Vehicle>();


        public void Move(Player me, World world, Game game, Move move)
        {
            initializeStrategy(world, game);
            initializeTick(me, world, game, move);

            if (me.RemainingActionCooldownTicks > 0)
            {
                return;
            }

            if (executeDelayedMove())
            {
                return;
            }

            myMove();

            executeDelayedMove();
        }

        private void initializeStrategy(World world, Game game)
        {
            if (random == null)
            {
                random = new RandomLong(game.RandomSeed);
                r = new Random();//random.NextInt(Convert.ToInt32(game.RandomSeed)));

                terrainTypeByCellXY = world.TerrainByCellXY;
                weatherTypeByCellXY = world.WeatherByCellXY;
            }

        }

        private void initializeTick(Player me, World world, Game game, Move move)
        {
            this.me = me;
            this.world = world;
            this.game = game;
            this.move = move;

            foreach (Vehicle vehicle in world.NewVehicles)
            {
                vehicleById.Add(vehicle.Id, vehicle);
                updateTickByVehicleId.Add(vehicle.Id, world.TickIndex);
            }

            foreach (VehicleUpdate vehicleUpdate in world.VehicleUpdates)
            {
                long vehicleId = vehicleUpdate.Id;

                if (vehicleUpdate.Durability == 0)
                {
                    vehicleById.Remove(vehicleId);
                    updateTickByVehicleId.Remove(vehicleId);
                }
                else
                {
                    if (vehicleById.ContainsKey(vehicleId))
                    {
                        vehicleById[vehicleId] = new Vehicle(vehicleById[vehicleId], vehicleUpdate);
                    }
                    else
                    {
                        vehicleById.Add(vehicleId, new Vehicle(vehicleById[vehicleId], vehicleUpdate));
                    }

                    if (updateTickByVehicleId.ContainsKey(vehicleId))
                    {
                        updateTickByVehicleId[vehicleId] = world.TickIndex;
                    }
                    else
                    {
                        updateTickByVehicleId.Add(vehicleId, world.TickIndex);
                    }
                }
            }
        }

        private bool executeDelayedMove()
        {
            if (delayedMoves.Count == 0)
            {
                return false;
            }

            AnotherMove anotherMove = delayedMoves.Dequeue();

            anotherMove(move);
            return true;
        }

        private static VehicleType getPreferredTargetType(VehicleType vehicleType)
        {
            switch (vehicleType)
            {
                case VehicleType.Fighter:
                    return VehicleType.Helicopter;

                case VehicleType.Helicopter:
                    return VehicleType.Tank;

                case VehicleType.Ifv:
                    return VehicleType.Helicopter;

                case VehicleType.Tank:
                    return VehicleType.Ifv;

                default:
                    return VehicleType.Arrv;
            }
        }
        /**
         * Основная логика нашей стратегии.
         */
        private void myMove()
        {
            // Каждые 300 тиков ...
            if (world.TickIndex % 300 == 0)
            {
                // ... для каждого типа техники ...
                foreach (VehicleType vehicleType in Enum.GetValues(typeof(VehicleType)))
                {
                    VehicleType targetType = getPreferredTargetType(vehicleType);

                    // ... если это брэм ...
                    if (targetType == VehicleType.Arrv)
                    {
                        continue;
                    }

                    // ... получаем центр формации ...
                    var s = streamVehicles(Ownership.Ally, vehicleType);

                    double formationCenterX = s.Count() == 0 ? Double.NaN : s.Average(v => v.X);
                    double formationCenterY = s.Count() == 0 ? Double.NaN : s.Average(v => v.Y);

                    // ... получаем центр формации противника или центр мира ...
                    var eS = streamVehicles(Ownership.Enemy, targetType);

                    double targetX = eS.Count() == 0 ? world.Width / 2.0d : eS.Average(v => v.X);
                    double targetY = eS.Count() == 0 ? world.Height / 2.0d : eS.Average(v => v.Y);

                    // .. и добавляем в очередь отложенные действия для выделения и перемещения техники.
                    if (!Double.IsNaN(formationCenterX) && !Double.IsNaN(formationCenterY))
                    {
                        delayedMoves.Enqueue(move =>
                        {
                            move.Action = ActionType.ClearAndSelect;
                            move.Right = world.Width;
                            move.Bottom = world.Height;
                            move.VehicleType = vehicleType;
                        });

                        delayedMoves.Enqueue(move =>
                        {
                            move.Action = ActionType.Move;
                            move.X = targetX - formationCenterX;
                            move.Y = targetY - formationCenterY;
                        });
                    }
                }

                // Также находим центр формации наших БРЭМ ...
                var sArrv = streamVehicles(Ownership.Ally, VehicleType.Arrv);


                double arrvFormationCenterX = sArrv.Count() == 0 ? Double.NaN : sArrv.Average(v => v.X);
                double arrvFormationCenterY = sArrv.Count() == 0 ? Double.NaN : sArrv.Average(v => v.Y);

                // .. и отправляем их в центр мира.
                if (!Double.IsNaN(arrvFormationCenterX) && !Double.IsNaN(arrvFormationCenterY))
                {
                    delayedMoves.Enqueue(Move =>
                    {
                        move.Action = ActionType.ClearAndSelect;
                        move.Right = world.Width;
                        move.Bottom = world.Height;
                        move.VehicleType = VehicleType.Arrv;
                    });

                    delayedMoves.Enqueue(Move =>
                    {
                        move.Action = ActionType.Move;
                        move.X = world.Width / 2.0d - arrvFormationCenterX;
                        move.Y = world.Height / 2.0D - arrvFormationCenterY;
                    });
                }

                return;
            }

            // Если ни один наш юнит не мог двигаться в течение 60 тиков ...
            var allyS = streamVehicles(Ownership.Ally);

            if (allyS.All(v => (world.TickIndex - updateTickByVehicleId[v.Id]) > 60))
            {
                /// ... находим центр нашей формации ...
                double allySX = allyS.Count() == 0 ? Double.NaN : allyS.Average(v => v.X);
                double allySY = allyS.Count() == 0 ? Double.NaN : allyS.Average(v => v.X);

                // ... и поворачиваем её на случайный угол.
                if (!Double.IsNaN(allySX) && !Double.IsNaN(allySY))
                {
                    move.Action = ActionType.Rotate;
                    move.X = allySX;
                    move.Y = allySY;
                    move.Angle = Math.PI * (2 * r.NextDouble() - 1);
                }
            }
        }


        private IEnumerable<Vehicle> streamVehicles(Ownership ownership, VehicleType vehicleType)
        {
            IEnumerable<Vehicle> stream = streamVehicles(ownership);

            stream = stream.Where(v => v.Type == vehicleType);

            return stream;
        }

        private IEnumerable<Vehicle> streamVehicles(Ownership ownership)
        {
            IEnumerable<Vehicle> stream = vehicleById.Select(v => v.Value);

            if (ownership == Ownership.Ally)
            {
                stream = stream.Where(v => v.PlayerId == me.Id);
            }
            else if (ownership == Ownership.Enemy)
            {
                stream = stream.Where(v => v.PlayerId != me.Id);
            }

            return stream;
        }

        private IEnumerable<Vehicle> streamVehicles()
        {
            return streamVehicles(Ownership.Any);
        }

        private enum Ownership
        {
            Any,
            Ally,
            Enemy
        }


    }

    public sealed class RandomLong
    {
        private long _seed;

        private const long LARGE_PRIME = 0x5DEECE66DL;
        private const long SMALL_PRIME = 0xBL;

        public RandomLong(long seed)
        {
            _seed = (seed ^ LARGE_PRIME) & ((1L << 48) - 1);
        }

        public int NextInt(int n)
        {
            if (n <= 0)
                throw new ArgumentOutOfRangeException("n", n, "n must be positive");

            if ((n & -n) == n)  // i.e., n is a power of 2
                return (int)((n * (long)next(31)) >> 31);

            int bits, val;

            do
            {
                bits = next(31);
                val = bits % n;
            } while (bits - val + (n - 1) < 0);
            return val;
        }

        private int next(int bits)
        {
            _seed = (_seed * LARGE_PRIME + SMALL_PRIME) & ((1L << 48) - 1);
            return (int)(((uint)_seed) >> (48 - bits));
        }
    }

}
