using System;
using Esnaf.Core;
using Esnaf.Domain.Content;
using Esnaf.Domain.Economy;
using Esnaf.Domain.Products;

namespace Esnaf.Domain.Appraisal
{
    /// <summary>
    /// Ekspertiz sistemi (GDD v0.3 3.2): ücreti öder, sonucu üretir ve kilitler.
    /// Kurallar: yalnızca pazardaki ürün; seviye açılış günü ve ekipman kilidi; ücret segmente göre, ANINDA ödenir ve "bekleyen"dir
    /// (ürün alınırsa maliyete eklenir, alınmazsa gider yazılır); aynı ürün + aynı seviye = aynı sonuç, ücret bir kez alınır (I5).
    /// Sonuç, <c>hash(masterSeed, instanceId, level)</c> tohumundan üretilir. Başarısızlıkta hiçbir durum değişmez.
    /// </summary>
    public sealed class AppraisalService
    {
        private readonly ContentDatabase _content;
        private readonly KnowledgeState _knowledge;
        private readonly EquipmentState _equipment;
        private readonly EconomyService _economy;
        private readonly InstanceStore _store;
        private readonly IdGenerator _resultIds;
        private readonly IEventBus _events;
        private readonly ulong _masterSeed;
        private readonly AppraisalCalculator _calculator;

        public AppraisalService(
            ContentDatabase content,
            KnowledgeState knowledge,
            EquipmentState equipment,
            EconomyService economy,
            InstanceStore store,
            IdGenerator resultIds,
            IEventBus events,
            ulong masterSeed)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (knowledge == null)
            {
                throw new ArgumentNullException(nameof(knowledge));
            }

            if (equipment == null)
            {
                throw new ArgumentNullException(nameof(equipment));
            }

            if (economy == null)
            {
                throw new ArgumentNullException(nameof(economy));
            }

            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            if (resultIds == null)
            {
                throw new ArgumentNullException(nameof(resultIds));
            }

            _content = content;
            _knowledge = knowledge;
            _equipment = equipment;
            _economy = economy;
            _store = store;
            _resultIds = resultIds;
            _events = events;
            _masterSeed = masterSeed;
            _calculator = new AppraisalCalculator(new ValueCalculator(content.ValueTables), content.Appraisal);
        }

        public Result<AppraisalResult> Appraise(long instanceId, string levelId, int day)
        {
            if (day < 1)
            {
                return Result<AppraisalResult>.Fail("day.invalid", "Day must be at least 1.");
            }

            ProductInstance instance;
            if (!_store.TryGet(instanceId, out instance))
            {
                return Result<AppraisalResult>.Fail("instance.unknown", "Unknown product instance " + instanceId + ".");
            }

            if (instance.Location != ProductLocation.Market)
            {
                return Result<AppraisalResult>.Fail("appraisal.not_on_market", "Only items on the market can be appraised.");
            }

            AppraisalLevel level;
            if (!_content.Appraisal.TryGetLevel(levelId, out level))
            {
                return Result<AppraisalResult>.Fail("appraisal.level_unknown", "Unknown appraisal level '" + levelId + "'.");
            }

            AppraisalResult existing;
            if (_knowledge.TryGet(instanceId, levelId, out existing))
            {
                return Result<AppraisalResult>.Ok(existing);
            }

            if (day < level.UnlockDay)
            {
                return Result<AppraisalResult>.Fail("appraisal.level_locked", "Level " + levelId + " opens on day " + level.UnlockDay + ".");
            }

            if (level.RequiredEquipment != null && !_equipment.Owns(level.RequiredEquipment))
            {
                return Result<AppraisalResult>.Fail("appraisal.equipment_missing", "Level " + levelId + " needs the " + level.RequiredEquipment + ".");
            }

            ProductDefinition definition = _content.GetProduct(instance.DefinitionId);
            Money fee = level.FeeFor(definition.Segment);
            ulong seed = AppraisalSeed.Compute(_masterSeed, instanceId, levelId);
            AppraisalEvaluation evaluation = _calculator.Evaluate(instance, definition, level, new PcgRandom(seed, AppraisalSeed.Stream));

            if (fee.IsPositive)
            {
                Result<TransactionRecord> paid = _economy.PayAppraisal(instanceId, instance.DefinitionId, fee, day);
                if (paid.IsFailure)
                {
                    return Result<AppraisalResult>.Fail(paid.ErrorCode, paid.Message);
                }
            }

            var result = new AppraisalResult(
                _resultIds.Next(),
                instanceId,
                instance.DefinitionId,
                levelId,
                day,
                fee,
                seed,
                evaluation.Findings,
                evaluation.BatteryRange,
                evaluation.BodyRange,
                evaluation.ValueRange,
                evaluation.Cards);
            _knowledge.Add(result);

            if (_events != null)
            {
                _events.Publish(new AppraisalCompleted(result.ResultId, instanceId, levelId, day, fee));
            }

            return Result<AppraisalResult>.Ok(result);
        }
    }
}
