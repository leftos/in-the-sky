-- The fault the drive must report rather than skip: the trigger raises, so the host disables the module and there are
-- no facts at all. It must read as a broken module, never as one that simply never fired.
return {
  id = 'trigger-errors',
  phases = { 'cruise' },
  trigger = function(ctx)
    error('the trigger raises in every cabin')
  end,
  describe = function(facts)
    return 'A passenger is waving from the far end of the cabin.'
  end,
  choices = function(facts)
    return {
      { id = 'help', label = 'Step in and help', needs_crew = true, minutes = 2, quality = 0.8 },
      { id = 'leave', label = 'Leave it for now', needs_crew = false, minutes = 0, quality = 0.2 },
    }
  end,
  effects = function(facts, choice)
    return { { after_minutes = 0, target = 'subject', line = 'They nod and sit back.' } }
  end,
}
