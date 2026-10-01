-- The fault the parse check exists for: a consequence names a need the host does not know, so the reader faults and the
-- module is disabled rather than half-applied.
return {
  id = 'effects-do-not-parse',
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
    return 'A passenger keeps looking towards the galley.'
  end,
  choices = function(facts)
    return {
      { id = 'help', label = 'Bring them a drink', needs_crew = true, minutes = 2, quality = 0.8 },
      { id = 'leave', label = 'Leave it for now', needs_crew = false, minutes = 0, quality = 0.2 },
    }
  end,
  effects = function(facts, choice)
    if choice ~= 'help' then
      return { { after_minutes = 0, target = 'subject', line = 'They wait and watch the cart go past.' } }
    end

    return { { after_minutes = 1, target = 'subject', need = 'thirst', delta = -5 } }
  end,
}
