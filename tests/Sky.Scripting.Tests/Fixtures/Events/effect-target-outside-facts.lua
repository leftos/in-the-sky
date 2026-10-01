-- The fault the membership check exists for: a branch targets a passenger the trigger never recorded, so the host
-- would drop the consequence when it lands and journal it as a moment with the reason.
return {
  id = 'effect-target-outside-facts',
  phases = { 'cruise' },
  trigger = function(ctx)
    if ctx.stage ~= 'cruise' then
      return nil
    end

    local subject = ctx:passenger(1)
    if subject == nil then
      return nil
    end

    return { subject = subject }
  end,
  describe = function(facts)
    return 'A passenger is asking to be moved away from the engines.'
  end,
  choices = function(facts)
    return {
      { id = 'help', label = 'Find them another seat', needs_crew = true, minutes = 3, quality = 0.7 },
      { id = 'leave', label = 'Leave it for now', needs_crew = false, minutes = 0, quality = 0.3 },
    }
  end,
  effects = function(facts, choice)
    if choice ~= 'help' then
      return { { after_minutes = 0, target = 'subject', line = 'They stay where they are, ears covered.' } }
    end

    -- The neighbour is a real passenger of the cabin, but the facts recorded only the subject.
    return { { after_minutes = 1, target = facts.subject + 1, need = 'unease', delta = -5 } }
  end,
}
