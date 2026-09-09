# DCD

## Domain

✅ MaintenanceStatus and CleaningStatus as dedicated enum classes  
✅ Clear association from tasks to their status enums  
✅ Realistic status values (e.g., PENDING, IN_PROGRESS, COMPLETED, CANCELLED)  
✅ Embedded in the domain package for consistency  
✅ PriceList — immutable catalog of base prices & rules  
✅ RoomPriceSnapshot — concrete price applied for a specific booking (captures time-of-booking rate, discounts, coupons)  
✅ Keeps RoomPrice as active pricing (for UI / current availability), but not used directly in bookings  

```plantuml
@startuml
package "domain" {

  ' === Entities ===
  class Guest {
    +UUID id
    +String firstName
    +String lastName
    +String email
    +String phoneNumber
    +String nationality
    +String passport
    +String ic
  }

  class Booking {
    +UUID id
    +int BookingRef
    +LocalDate startAt
    +LocalDate endAt
    +LocalDateTime checkInAt
    +LocalDateTime checkOutAt
    +boolean isCancelled
    +int adults
    +int children
    +int babies
    +String marketSegment
    +String mealPlan
  }

  class Room {
    +UUID id
    +String roomNumber
    +int floor
    +String variant
    +int capacity
    +RoomStatus status
  }

  class PriceList {
    +UUID id
    +String name        e.g., "2024 Summer Rates", "Weekend Special"
    +LocalDate validFrom
    +LocalDate validUntil
    +Boolean isDefault  ' One default per room/variant
    +String description
  }

  class RoomPrice {
    +UUID id
    +Room room
    +BigDecimal basePricePerNight
    +Currency currency = "USD"
    +PriceType priceType
    +LocalDate validFrom
    +LocalDate validUntil ?   ' null = currently active
    +String note              ' e.g., "Early Bird 10% off", "Last Minute Deal"
  }

  class RoomPriceSnapshot {
    +UUID bookingId
    +UUID roomPriceId      ' Which PriceList/RoomPrice was used?
    +BigDecimal pricePerNight
    +Currency currency = "USD"
    +LocalDate date         ' For per-night breakdown (e.g., Mon=low, Sat=high)
    +BigDecimal discountAmount = 0
    +BigDecimal couponDiscount = 0
    +String appliedCouponCode ?   ' e.g., "WELCOME10", null if none
    +String description           ' e.g., "Base + Weekend Surcharge - $50"
  }

  class Staff {
    +UUID id
    +String firstName
    +String lastName
    +String phoneNumber
    +String initials
    +Role role
  }

  class CleaningTask {
    +UUID id
    +LocalDateTime cleanStartAt
    +LocalDateTime cleanEndAt ?
    +String info
    +CleaningStatus status = ToDo
  }

  class MaintenanceTask {
    +UUID id
    +String note
    +String updatedNote
    +LocalDate startAt
    +LocalDate finishAt ?
    +Priority priority
    +MaintenanceStatus status = ToDo
  }

  ' === Status Enums ===
  enum MaintenanceStatus {
    PENDING
    IN_PROGRESS
    COMPLETED
    CANCELLED
    FAILED
  }

  enum CleaningStatus {
    NOT_SCHEDULED
    SCHEDULED
    IN_PROGRESS
    COMPLETED
    REJECTED
  }

  ' New: PriceList (static/rarely-changed catalog)
  enum PriceType {
    BASE
    WEEKEND
    HOLIDAY
    PROMO
    SEASONAL_HIGH
    SEASONAL_LOW
  }
}

' === Relationships ===
Guest "1" --> "1..*" Booking : makes >
Booking "1" --> "0..*" RoomPriceSnapshot : includes >   ' ← per night!
Booking "1" --> "1" Room : occupies >

Room "1" *-- "0..*" RoomPrice : has active pricing history >
Room "1" *-- "0..*" PriceList : belongs to list (optional, e.g., by variant)
Room "1" --> "0..*" CleaningTask : requires >
Room "1" --> "0..*" MaintenanceTask : may require >
RoomPrice "1" --> "1" PriceList : part of >   ' if you want strict association

' Note: RoomPriceSnapshot is built from RoomPrice + coupon/offer at booking time
RoomPriceSnapshot "1" --> "1" RoomPrice : copied from >
RoomPriceSnapshot "0..*" --> "1" Booking : belongs to >

CleaningTask "0..*" --> "1" Staff : assigned to >
MaintenanceTask "0..*" --> "1" Staff : assigned to >

' Enum associations (explicit links for clarity)
CleaningTask .> CleaningStatus : uses >
MaintenanceTask .> MaintenanceStatus : uses >
RoomPrice .> PriceType : uses >


' === Notes ===
note top of RoomPriceSnapshot
  **Per-night, immutable snapshot**
  - Captures exact price *as seen at checkout*, including discounts.
  - Allows full revenue reconciliation even if RoomPrice changes later.
  - Supports daily rate variations (e.g., weekend surcharge on Sat).
end note

note bottom of RoomPrice
  Active pricing — used for real-time availability & quoting.
  Not directly tied to bookings. Overlaps allowed via validFrom/validUntil.
end note
@enduml
```


💡 Example Flow:
1.  Admin creates a PriceList named "Summer 2024"
1.  Adds RoomPrices (e.g., Standard Room: $150, Suite: $300)
1.  Guest books → system copies relevant RoomPrice entries into RoomPriceSnapshot per night
1.  If guest applies "WELCOME10" coupon → couponDiscount = 15, appliedCouponCode = "WELCOME10"
1.  Later, RoomPrice changes to $160 — but booking stays accurate with snapshot.

## Application

## Infrastructure

## Web

## API
