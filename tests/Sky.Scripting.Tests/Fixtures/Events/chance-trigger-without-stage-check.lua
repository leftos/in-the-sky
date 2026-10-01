-- The fault the phase check exists for, on a trigger that rolls a chance the way the shipped modules do: the roll lands
-- on about one seed in fifty, so a check that offers one seed per stage would let this module through.
return {
  id = 'chance-trigger-without-stage-check',
  phases = { 'cruise' },
  trigger = function(ctx)
    if math.random() < 0.02 then
      local subject = ctx:passenger(1)
      if subject ~= nil then
        return { subject = subject }
      end
    end

    return nil
  end,
  describe = function(facts)
    return 'A passenger is watching the cabin lights come on.'
  end,
  choices = function(facts)
    return {
      { id = 'help', label = 'Reassure them', needs_crew = true, minutes = 2, quality = 0.7 },
      { id = 'leave', label = 'Leave it for now', needs_crew = false, minutes = 0, quality = 0.3 },
    }
  end,
  effects = function(facts, choice)
    if choice ~= 'help' then
      return { { after_minutes = 0, target = 'subject', line = 'They look away and settle.' } }
    end

    return { { after_minutes = 0, target = 'subject', line = 'The crew member explains each sound.' } }
  end,
}
